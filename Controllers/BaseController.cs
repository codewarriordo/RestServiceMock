using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using ServiceMock.Config;

namespace ServiceMock.Controllers;


public class BaseController : ControllerBase
{

    private Configurator mConfigurator;
    private ILogger<BaseController> mLogger;
    public BaseController(Configurator configurator, ILogger<BaseController> _logger)
    {
        mConfigurator = configurator;
        mLogger = _logger;
    }


    [HttpGet]
    public async Task<IActionResult> GetMethod(string id)
    {
        mLogger.LogTrace("-> GetMethod");
        var endpoint = mConfigurator.GetEndpointByPath(Request.Path.Value, RestMethod.GET);
        Response.StatusCode = (int)HttpStatusCode.OK;
        mLogger.LogInformation($"endpoint point served {endpoint.Path}");
        mLogger.LogTrace("<- GetMethod");
        if (endpoint.LongPoll)
        {
            var content = mConfigurator.GetCurrentEventContent(id, endpoint);
            return Content(content, "application/json");
        }
        else
        {
            return Content(mConfigurator.GetDynamicContent(endpoint), "application/json");
        }
    }

    [HttpPost]
    public async Task<IActionResult> PostMethod()
    {
        mLogger.LogDebug("-> PostMethod");
        var endpoint = mConfigurator.GetEndpointByPath(Request.Path.Value, RestMethod.POST);
        if (endpoint.ReturnCode == 0)
        {
            Response.StatusCode = (int)HttpStatusCode.OK;
        }
        else
        {
            Response.StatusCode = endpoint.ReturnCode;
        }
        mLogger.LogInformation($"endpoint point served {endpoint.Path}");
        mLogger.LogDebug("-> PostMethod");
        return Content(mConfigurator.GetDynamicContent(endpoint), "application/json");
    }
     [HttpDelete]
    public async Task<IActionResult> DeleteMethod()
    {
        mLogger.LogDebug("-> DeleteMethod");
        var endpoint = mConfigurator.GetEndpointByPath(Request.Path.Value,RestMethod.DELETE);
        if (endpoint.ReturnCode == 0)
        {
            Response.StatusCode = (int)HttpStatusCode.OK;
        }
        else
        {
            Response.StatusCode = endpoint.ReturnCode;
        }
        mLogger.LogInformation($"endpoint point served {endpoint.Path}");
        mLogger.LogDebug("-> DeleteMethod");
        return Content(mConfigurator.GetDynamicContent(endpoint), "application/json");
    }
}
