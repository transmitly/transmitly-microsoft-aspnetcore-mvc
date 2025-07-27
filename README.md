# Transmitly.Microsoft.AspnetCore.Mvc

A [Transmitly](https://github.com/transmitly/transmitly) utility package for handling registration and controllers for channel provider delivery reports.

### Getting started

To use install the [NuGet package](https://github.com/transmitly/transmitly-microsoft-aspnetcore-mvc):

```shell
dotnet add package Transmitly.Microsoft.AspnetCore.Mvc
```

Then add the model binders using `AddChannelProviderDeliveryReportModelBinders()`:

```csharp
using Transmitly;
...
 public static void Main(string[] args)
{
	var builder = WebApplication.CreateBuilder(args);
	builder.Services
	.AddControllers(options =>
	{
		//Adds the necessary model binders to handle channel provider specific webhooks (Twilio, Infobip, etc)
		//and convert them to delivery reports
		options.AddTransmitlyDeliveryReportModelBinders();
	})
	.AddTransmitly(tly => {...});
}
```



# Using Default Delivery Report Controller
Inheriting the ChannelProviderDeliveryReportController will setup an `POST` route named, `HandleDeliveryReport` (example: `https://yourapp.com/Communications/HandleDeliveryReport`) that will automatically trigger
your registered delivery report handlers for the provided ICommunicationsClient.

The `HandleDeliveryReport` method can be overridden. Allowing you to customize behaviors and set route specifics.

MyDeliveryReportsController.cs
```csharp 
using System;
using System.Web.Mvc;
using Transmitly;
using Transmitly.Delivery;

namespace Transmitly.Aspnet.Mvc.Examples
{
	[AllowAnonymous]
	public class CommunicationsController : ChannelProviderDeliveryReportController
	{
		public CommunicationsController(ICommunicationsClient communicationsClient) : base(communicationsClient)
		{
		}
	}
}
```
Or if you prefer to implement it yourself
```csharp
[HttpPost("channel/provider/update", Name = "DeliveryReport")]
public IActionResult ChannelProviderDeliveryReport(ChannelProviderDeliveryReportRequest providerReport)
{
	_communicationsClient.DispatchAsync(providerReport.DeliveryReports);
	return Ok();
}
```
* See the [Transmitly](https://github.com/transmitly/transmitly) project for more details on how use and configure the library.


<picture>
  <source media="(prefers-color-scheme: dark)" srcset="https://github.com/transmitly/transmitly/assets/3877248/524f26c8-f670-4dfa-be78-badda0f48bfb">
  <img alt="an open-source project sponsored by CiLabs of Code Impressions, LLC" src="https://github.com/transmitly/transmitly/assets/3877248/34239edd-234d-4bee-9352-49d781716364" width="350" align="right">
</picture> 

---------------------------------------------------

_Copyright &copy; Code Impressions, LLC - Provided under the [Apache License, Version 2.0](http://apache.org/licenses/LICENSE-2.0.html)._
