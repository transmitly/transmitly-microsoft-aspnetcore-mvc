// ﻿﻿Copyright (c) Code Impressions, LLC. All Rights Reserved.
//  
//  Licensed under the Apache License, Version 2.0 (the "License")
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//  
//      http://www.apache.org/licenses/LICENSE-2.0
//  
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.

using Microsoft.AspNetCore.Mvc;
using System.Net;
using Transmitly.Delivery;
using System.Linq;
using Transmitly.Util;

namespace Transmitly;

/// <summary>
/// Base class for registering a single route for handling incoming channel provider delivery reports.
/// </summary>
/// <param name="communicationsClient">Communications client.</param>
public abstract class ChannelProviderDeliveryReportController(ICommunicationsClient communicationsClient) : ControllerBase
{
	private readonly ICommunicationsClient _communicationsClient = Guard.AgainstNull(communicationsClient);

	/// <summary>
	/// Handle an incoming channel provider delivery report. Default behavior triggers delivery report handlers.
	/// </summary>
	/// <param name="request">Channel provider delivery report.</param>
	/// <returns>OK; Otherwise BadRequest with errors.</returns>
	[HttpPost]
	public virtual ActionResult HandleDeliveryReport(ChannelProviderDeliveryReportRequest request)
	{
		if (ModelState.IsValid && request != null && request.DeliveryReports != null)
		{
			_communicationsClient.DispatchAsync(request.DeliveryReports);
			return Ok();
		}

		var errorString = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors));
		return StatusCode((int)HttpStatusCode.InternalServerError, new ProblemDetails
		{
			Detail = errorString,
			Status = (int)HttpStatusCode.InternalServerError,
			Title = "Unexpected Error"
		});
	}
}