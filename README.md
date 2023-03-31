# RestServiceMock
REST WebService Mock including Webhook function 
The main goal of this project was to simulate a service platform to make performance tests of the own depending microservices.
For example you have a microservice which is connected to MS Graph API to monitor MS Teams user states. In the real world it's hard to simulate thousands of users to test the own implementation 

## Summary
* configurable endpoints POST, GET, DELETE
* endpoints have JSON response objects saved in files
* JSON responses can include dynamics arrays
* JSON objects can have dynamic elements like timestamps, enumerations, random integer values
* Webhook support with configurable event sequences

## Configuration
The current configuration is set with the environment CONFIG_FILE, see launchSettings.json
In this **baseConfiguration.json** are the endpoints, webhook and loop identifier
## Example
The project includes a example configuration.
Start the service and there are 2 endpoints available
* https://localhost:5001/api/V1.0/users
  + return 30 users with different names 
* https://localhost:5001/api/V1.0/users/states
  + returns the states for the 30 users, the states are calculated from a random enumeration

In the configuration is also a webhook event sequence defined. 
Every second the webhook is called for a random user including his state state
To get it running you only need a webhook simulation URL from https://webhook.site and than change the target URL in **baseConfiguration.json**




