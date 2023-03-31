using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Newtonsoft.Json.Linq;
using ServiceMock.Configuration.Model;
using ServiceMock.Extension;
using ServiceMockTemp.Configuration.Model;

namespace ServiceMock.Config
{
    public class Configurator
    {
        JsonSerializerOptions mSerializerOptions;
        private BaseConfig mBaseConfiguration;
        public BaseConfig BaseConfiguration => mBaseConfiguration;
        private Random mRandom = new Random();
        private ILogger<Configurator> mLogger;
        private Dictionary<string, EventEndpoint> mIdToEventEnpoint = new Dictionary<string, EventEndpoint>();

        private List<StaticEndpoint> mStaticEndpoints = new List<StaticEndpoint>();

        Thread WebHookThread;
        IHttpClientFactory mHttpClientFactory;

        public Configurator(ILogger<Configurator> _logger, IHttpClientFactory httpClientFactory)
        {
            mLogger = _logger;
            mHttpClientFactory = httpClientFactory;
        }

        internal void Init()
        {
            mLogger.LogTrace("-> init");
            mSerializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true,
                Converters ={
                new JsonStringEnumConverter()
                }
            };
            mSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            var file = Environment.GetEnvironmentVariable("CONFIG_FILE");
            mLogger.LogWarning($"read config file ${file}");
            string contents = File.ReadAllText(file);
            mBaseConfiguration = JsonSerializer.Deserialize<BaseConfig>(contents, mSerializerOptions);
            foreach (Endpoint endpoint in mBaseConfiguration.Endpoints)
            {
                if (endpoint.ResponseFile != null && !endpoint.LongPoll)
                {
                    endpoint.ResponseContent = File.ReadAllText(endpoint.ResponseFile);
                    CheckDynamicContent(endpoint);
                }
                else if (endpoint.LongPoll)
                {
                    endpoint.ResponseContent = File.ReadAllText(endpoint.ResponseFile);
                    //readEventConfig(endpoint);
                }
            }
            if (mBaseConfiguration.WebHooks != null)
            {
                foreach (WebHook webHook in mBaseConfiguration.WebHooks)
                {
                    ReadWebHookConfig(webHook);
                    Thread myNewThread = new Thread(() => RunWebHook(webHook));
                    myNewThread.Start();
                }
            }
            mLogger.LogTrace("<- init");
        }

        private void RunWebHook(WebHook webHook)
        {
            while (true)
            {
                var events = GetCurrentWebHookContent(webHook);
                foreach (Event currentEvent in events)
                {
                    try
                    {
                        using var httpClient = mHttpClientFactory.CreateClient();
                        ManipulateDynamicPropertys(currentEvent.DynamicPropertys);
                        var content = new StringContent(currentEvent.EventTemplate.ToString(), Encoding.UTF8, "application/json");
                        var result = httpClient.PostAsync(webHook.TargetUrl, content).Result;
                    }
                    catch (Exception exception)
                    {
                        if (exception.InnerException.GetType() == typeof(HttpRequestException))
                        {
                            mLogger.LogInformation($"webhook endpoint not up {webHook.TargetUrl}");
                        }
                        else
                        {
                            mLogger.LogError(exception, "");
                        }

                    }

                }
            }
        }

        private StaticEndpoint CheckDynamicContent(Endpoint endpoint)
        {
            if (mLogger.IsEnabled(LogLevel.Trace))
                mLogger.LogTrace("-> checkDynamicContent");
            var returnValue = new StaticEndpoint() { EndpointConfig = endpoint };
            mStaticEndpoints.Add(returnValue);
            JObject jo = JObject.Parse(endpoint.ResponseContent);
            returnValue.ContentObject = jo;
            if (endpoint.ResponseContent.Contains("sm_"))
            {
                mLogger.LogInformation($"endpoint {endpoint.Path} must be manipulated");

                List<JArray> allArrays = new List<JArray>();
                FindArray(jo, allArrays);
                foreach (JArray currentArray in allArrays)
                {
                    JObject arrayDescriptionElement = (JObject)currentArray.First;
                    if (arrayDescriptionElement.Property("sm_filename") != null)
                    {
                        currentArray.First.Remove();
                        var fileName = arrayDescriptionElement.Property("sm_filename").Value.ToString();
                        string contents = File.ReadAllText(fileName);
                        JObject elementTemplate = JObject.Parse(contents);
                        if (arrayDescriptionElement.Property("sm_loopIdMapping") != null)
                        {
                            List<LoopIdMapping> mappings = GetLoopIdMapping((JArray)arrayDescriptionElement.Property("sm_loopIdMapping").Value);
                            List<DynamicProperty> dynamicProperties = GenerateLoopData(elementTemplate, currentArray, mappings);
                            returnValue.DynamicPropertys = dynamicProperties;

                        }
                    }
                }

                if (mLogger.IsEnabled(LogLevel.Debug))
                    mLogger.LogDebug("manipulate " + returnValue);

            }
            else if (endpoint.ResponseContent.Contains("%TIMESTAMP%") || endpoint.ResponseContent.Contains("%UUID%") || endpoint.ResponseContent.Contains("%ENUM%"))
            {
                returnValue.DynamicPropertys = FindDynamicsPropertys(returnValue.ContentObject);
                if (mLogger.IsEnabled(LogLevel.Debug))
                    mLogger.LogDebug("manipulate properties " + returnValue);
            }
            mLogger.LogTrace("<- checkDynamicContent");
            return returnValue;

        }

        public string GetDynamicContent(Endpoint endpoint)
        {
            if (mLogger.IsEnabled(LogLevel.Trace))
                mLogger.LogTrace("-> GetDynamicContent");
            var foundEndpoint = mStaticEndpoints.Find(staticEndpoint => (staticEndpoint.EndpointConfig.Path.Equals(endpoint.Path)));
            ManipulateDynamicPropertys(foundEndpoint.DynamicPropertys);
            var returnValue = foundEndpoint.ContentObject.ToString();

            mLogger.LogTrace("<- GetDynamicContent");
            return returnValue;

        }


        private List<DynamicProperty> GenerateLoopData(JObject elementTemplate, JArray currentArray, List<LoopIdMapping> mappings)
        {
            if (mLogger.IsEnabled(LogLevel.Trace))
                mLogger.LogTrace("-> GenerateLoopData");
            List<DynamicProperty> returnValue = new List<DynamicProperty>();
            var loopMapping1 = mappings[0];
            for (int i = loopMapping1.Identifier.StartValue; i <= loopMapping1.Identifier.StopValue; i++)
            {
                JObject clone = (JObject)elementTemplate.DeepClone();
                clone.Property(loopMapping1.PropertyName).Value = i;
                SetLoopIdInValues(clone, i);
                if (mappings.Count > 1)
                {
                    var loopMapping2 = mappings[1];
                    for (int a = loopMapping2.Identifier.StartValue; a <= loopMapping2.Identifier.StopValue; a++)
                    {
                        JObject clone2 = (JObject)clone.DeepClone();
                        clone2.Property(loopMapping2.PropertyName).Value = a;
                        currentArray.Add(clone2);
                    }

                }
                else
                {
                    currentArray.Add(clone);
                }
                returnValue.AddRange(FindDynamicsPropertys(clone));
            }
            mLogger.LogTrace("<- GenerateLoopData");
            return returnValue;

        }

        private List<LoopIdMapping> GetLoopIdMapping(JArray loopArray)
        {
            var returnList = new List<LoopIdMapping>();
            foreach (JObject currentObject in loopArray)
            {
                var identifierName = currentObject.Property("Identifier").Value.ToString();
                var propertyName = currentObject.Property("PropertyName").Value.ToString();
                var IdentifierObject = mBaseConfiguration.LoopIdentifier.Find(identifier => identifier.Name.Equals(identifierName));
                returnList.Add(new LoopIdMapping
                {
                    Identifier = IdentifierObject,
                    PropertyName = propertyName
                });
            }
            return returnList;
        }


        private void SetLoopIdInValues(JToken clone, int i = 0)
        {
            var allElements = clone.Children().GetEnumerator();
            while (allElements.MoveNext())
            {
                JProperty currentElement = (JProperty)allElements.Current;
                if (currentElement.Value.Type == JTokenType.String)
                {
                    if (currentElement.Value.ToString().Contains("%i%"))
                    {
                        currentElement.Value = new JValue(currentElement.Value.ToString().Replace("%i%", "" + i));
                    }
                }

            }

        }
        private void ManipulateDynamicPropertys(List<DynamicProperty> properties)
        {
            if (properties != null)
            {
                foreach (DynamicProperty prop in properties)
                {
                    switch (prop.Type)
                    {
                        case DynamicType.TIMESTAMP:
                            prop.JsonProperty.Value = new JValue(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
                            break;
                        case DynamicType.UUID:
                            prop.JsonProperty.Value = Guid.NewGuid().ToString();
                            break;
                        case DynamicType.ENUMERATION:
                            int randomValue = mRandom.Next(prop.PossibleEnumValues.Length);
                            prop.JsonProperty.Value = new JValue(prop.PossibleEnumValues[randomValue]);
                            break;
                        case DynamicType.LOOP_IDENTIFIER:
                            randomValue = mRandom.Next(prop.LoopIdent.StopValue);
                            prop.JsonProperty.Value = new JValue(randomValue);
                            break;
                    }
                }

            }
        }
        private List<DynamicProperty> FindDynamicsPropertys(JToken clone)
        {
            var returnValue = new List<DynamicProperty>();
            var allElements = clone.Children().GetEnumerator();
            while (allElements.MoveNext())
            {
                JProperty currentElement = (JProperty)allElements.Current;
                var loopIdentifier = mBaseConfiguration.LoopIdentifier.Find(identifier => identifier.Name.Equals(currentElement.Name));
                if(loopIdentifier is not null)
                {
                    returnValue.Add(new DynamicProperty { Type = DynamicType.LOOP_IDENTIFIER, JsonProperty = currentElement, LoopIdent = loopIdentifier});
                }
                if (currentElement.Value.Type == JTokenType.String)
                {
                    if (currentElement.Value.ToString().Contains("%i%"))
                    {
                        returnValue.Add(new DynamicProperty { Type = DynamicType.INT, JsonProperty = currentElement });
                    }
                    else if (currentElement.Value.ToString().Contains("%TIMESTAMP%"))
                    {
                        returnValue.Add(new DynamicProperty { Type = DynamicType.TIMESTAMP, JsonProperty = currentElement });
                    }
                    else if (currentElement.Value.ToString().Contains("%UUID%"))
                    {
                        returnValue.Add(new DynamicProperty { Type = DynamicType.UUID, JsonProperty = currentElement });
                    }
                }
                if (currentElement.Value.Type == JTokenType.Integer)
                {
                    int value = Int32.Parse(currentElement.Value.ToString());
                    if (value < 0)
                    {
                        returnValue.Add(new DynamicProperty { Type = DynamicType.INT, JsonProperty = currentElement });
                    }
                }
                if (currentElement.Value.Type == JTokenType.Object)
                {
                    //check if the value is part of a enumeration
                    JObject currentObject = (JObject)currentElement.Value;
                    if (currentObject["%ENUM%"] != null)
                    {
                        JValue enumeration = (JValue)currentObject["%ENUM%"];
                        var possibleValues = enumeration.Value.ToString().Split(",");
                        returnValue.Add(new DynamicProperty { Type = DynamicType.ENUMERATION, JsonProperty = currentElement, PossibleEnumValues = possibleValues });

                    }
                }
            }
            return returnValue;

        }


        public EventEndpoint ReadEventConfig(Endpoint endpoint)
        {
            mLogger.LogDebug("-> readEventConfig");
            var returnValue = new EventEndpoint()
            {
                EndpointConfig = endpoint
            };
            if (endpoint.ResponseContent.Contains("sm_"))
            {
                mLogger.LogInformation($"readEventConfig endpoint {endpoint.Path} must be manipulated");
                JObject jo = JObject.Parse(endpoint.ResponseContent);
                returnValue.ResponseDocument = jo;
                List<JArray> allArrays = new List<JArray>();
                FindArray(jo, allArrays);
                foreach (JArray currentArray in allArrays)
                {
                    returnValue.EventArray = currentArray;
                    JObject arrayDescriptionElement = (JObject)currentArray.First;
                    var fileName = arrayDescriptionElement.Property("sm_filename").Value.ToString();
                    string eventContent = File.ReadAllText(fileName);
                    returnValue.EventConfig = JsonSerializer.Deserialize<EventConfig>(eventContent, mSerializerOptions);
                    foreach (EventSequence eSequence in returnValue.EventConfig.EventSequences)
                    {
                        foreach (Event currentEvent in eSequence.Events)
                        {
                            currentEvent.EventContent = File.ReadAllText(currentEvent.FileName);
                            currentEvent.EventTemplate = JObject.Parse(currentEvent.EventContent);
                            currentEvent.DynamicPropertys = FindDynamicsPropertys(currentEvent.EventTemplate);
                        }
                    }
                }
                if (mLogger.IsEnabled(LogLevel.Debug))
                    mLogger.LogDebug("readEventConfig manipulate " + jo.ToString());

            }
            mLogger.LogDebug("<- readEventConfig");
            return returnValue;

        }
        public void ReadWebHookConfig(WebHook webHook)
        {
            mLogger.LogDebug("-> ReadWebHookConfig");
            var fileName = webHook.EventSequence;
            string eventContent = File.ReadAllText(fileName);
            webHook.EventConfig = JsonSerializer.Deserialize<EventConfig>(eventContent, mSerializerOptions);
            foreach (EventSequence eSequence in webHook.EventConfig.EventSequences)
            {
                foreach (Event currentEvent in eSequence.Events)
                {
                    currentEvent.EventContent = File.ReadAllText(currentEvent.FileName);
                    currentEvent.EventTemplate = JObject.Parse(currentEvent.EventContent);
                    currentEvent.DynamicPropertys = FindDynamicsPropertys(currentEvent.EventTemplate);
                }
            }
            mLogger.LogDebug("<- ReadWebHookConfig");
        }
        private void FindArray(JToken rootElement, List<JArray> allArrays)
        {
            var enumerator = rootElement.Children().GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (enumerator.Current.Type == JTokenType.Array)
                {
                    allArrays.Add((JArray)enumerator.Current);
                }
                FindArray(enumerator.Current, allArrays);
            }
        }

        public Endpoint GetEndpointByPath(string path, RestMethod method)
        {
            path = path.ToLower();
            var foundEndpoint = mBaseConfiguration.Endpoints.Find(endpoint => (endpoint.Path.CompareTo(path) == 0 && endpoint.Method == method));
            if (foundEndpoint == null)
            {
                //ok maybe with wildcard
                foreach (Endpoint endpoint in mBaseConfiguration.Endpoints)
                {
                    if (endpoint.Path.Contains("{id?}") && endpoint.Method == method)
                    {
                        var pathElements = endpoint.Path.Split("{id?}");
                        if (path.StartsWith(pathElements[0]))
                        {
                            if (pathElements.Length > 1)
                            {
                                if (path.EndsWith(pathElements[1])) return endpoint;
                            }
                            else
                            {
                                return endpoint;
                            }


                        }
                    }
                }
            }
            return foundEndpoint;
        }

        public string GetCurrentEventContent(String uniqueueId, Endpoint endpoint)
        {
            mLogger.LogDebug("-> GetCurrentEventContent");
            EventEndpoint eventEndpoint = null;
            if (!mIdToEventEnpoint.TryGetValue(uniqueueId, out eventEndpoint))
            {
                eventEndpoint = ReadEventConfig(endpoint);
                mIdToEventEnpoint[uniqueueId] = eventEndpoint;
            }
            eventEndpoint.EventArray.Clear();
            var currentSequence = FindCurrentSequence(eventEndpoint.EventConfig.EventSequences);
            List<Event> eventToProcess = new List<Event>();
            if (currentSequence != null)
            {
                foreach (Event currentEvent in currentSequence.Events)
                {
                    if (!currentEvent.Processed)
                    {
                        if (eventToProcess.Count == 0)
                        {
                            mLogger.LogInformation($"GetCurrentEventContent add event {currentEvent.FileName}");
                            eventToProcess.Add(currentEvent);

                        }
                        else
                        {
                            //check if same time out otherwise send in next cycle
                            if (currentEvent.TimeOffset == eventToProcess.First().TimeOffset)
                            {
                                mLogger.LogInformation($"GetCurrentEventContent add event {currentEvent.FileName}");
                                eventToProcess.Add(currentEvent);
                            }
                            else break;
                        }
                    }
                };
            }

            if (eventToProcess.Count == 0)
            {
                Thread.Sleep(5000);
            }
            else
            {
                if (eventToProcess.First().TimeOffset > 0)
                {
                    Thread.Sleep(eventToProcess.First().TimeOffset - currentSequence.LastOffset);
                    currentSequence.LastOffset = eventToProcess.First().TimeOffset;
                }
                eventToProcess.ForEach(currentEvent =>
                {
                    ManipulateDynamicPropertys(currentEvent.DynamicPropertys);
                    eventEndpoint.EventArray.Add(currentEvent.EventTemplate);
                    currentEvent.Processed = true;
                });
                //check if every event is processed loop is over and set alle events back
                if (!currentSequence.Events.Any(x => !x.Processed))
                {
                    currentSequence.CurrentLoop++;
                    currentSequence.LastOffset = 0;
                    currentSequence.Events.ForEach(x => x.Processed = false);
                }

            }
            var retValue = eventEndpoint.ResponseDocument.ToString();
            retValue = retValue.ReplaceFirst("%ID%", uniqueueId);
            if (mLogger.IsEnabled(LogLevel.Debug))
                mLogger.LogDebug("event content " + retValue);
            mLogger.LogDebug("<- GetCurrentEventContent");
            return retValue;

        }


        public List<Event> GetCurrentWebHookContent(WebHook webHook)
        {
            mLogger.LogDebug("-> GetCurrentEventContent");
            var currentSequence = FindCurrentSequence(webHook.EventConfig.EventSequences);
            List<Event> eventToProcess = new List<Event>();
            if (currentSequence != null)
            {
                foreach (Event currentEvent in currentSequence.Events)
                {
                    if (!currentEvent.Processed)
                    {
                        if (eventToProcess.Count == 0)
                        {
                            mLogger.LogInformation($"GetCurrentEventContent add event {currentEvent.FileName}");
                            eventToProcess.Add(currentEvent);
                        }
                        else
                        {
                            //check if same time out otherwise send in next cycle
                            if (currentEvent.TimeOffset == eventToProcess.First().TimeOffset)
                            {
                                mLogger.LogInformation($"GetCurrentEventContent add event {currentEvent.FileName}");
                                eventToProcess.Add(currentEvent);
                            }
                            else break;
                        }
                    }
                };
            }
            if (eventToProcess.Count == 0)
            {
                Thread.Sleep(5000);
            }
            else
            {
                if (eventToProcess.First().TimeOffset > 0)
                {
                    Thread.Sleep(eventToProcess.First().TimeOffset - currentSequence.LastOffset);
                    currentSequence.LastOffset = eventToProcess.First().TimeOffset;
                }
                eventToProcess.ForEach(currentEvent =>
                {
                    ManipulateDynamicPropertys(currentEvent.DynamicPropertys);
                    currentEvent.Processed = true;
                });
                //check if every event is processed loop is over and set alle events back
                if (!currentSequence.Events.Any(x => !x.Processed))
                {
                    currentSequence.CurrentLoop++;
                    currentSequence.LastOffset = 0;
                    currentSequence.Events.ForEach(x => x.Processed = false);
                }

            }

            mLogger.LogDebug("<- GetCurrentEventContent");
            return eventToProcess;

        }

        private EventSequence FindCurrentSequence(List<EventSequence> sequences)
        {
            foreach (EventSequence sequence in sequences)
            {
                if (sequence.CurrentLoop <= sequence.LoopCount)
                {
                    mLogger.LogInformation($"sequence found {sequence.Name}");
                    return sequence;
                }
            }
            return null;
        }
    }
}