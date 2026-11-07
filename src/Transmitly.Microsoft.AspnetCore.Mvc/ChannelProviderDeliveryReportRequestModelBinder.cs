// Copyright (c) Code Impressions, LLC. All Rights Reserved.
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

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Transmitly.ChannelProvider.Configuration;
using Transmitly.Delivery;
using System.Linq;

namespace Transmitly.Microsoft.Aspnet.Mvc;

class ChannelProviderDeliveryReportRequestModelBinder : IModelBinder
{
	public async Task BindModelAsync(ModelBindingContext bindingContext)
	{
		var factory = bindingContext.HttpContext.RequestServices.GetRequiredService<IChannelProviderFactory>();
		var request = bindingContext.HttpContext.Request;
		string? content = null;
		if (request.Body.CanRead)
		{
			request.EnableBuffering();
			using var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
			content = await reader.ReadToEndAsync().ConfigureAwait(false);
			if (request.Body.CanSeek)
				request.Body.Position = 0;
		}

		// MVC's form value provider reads the body before binding runs, so rebuild it from the parsed form
		// (form-signing providers such as Twilio need every posted field).
		if (string.IsNullOrEmpty(content) && request.HasFormContentType)
		{
			var form = await request.ReadFormAsync().ConfigureAwait(false);
			content = string.Join("&", form.SelectMany(f => f.Value.Select(v => Uri.EscapeDataString(f.Key) + "=" + Uri.EscapeDataString(v ?? string.Empty))));
		}

		var adaptorContext = new DefaultRequestAdaptorContext(request, content);

		DeliveryReportRequestValidationException? lastValidationRejection = null;
		foreach (var registration in await factory.GetAllDeliveryReportRequestAdaptorsAsync().ConfigureAwait(false))
		{
			var adaptor = await factory.ResolveDeliveryReportRequestAdaptorAsync(registration).ConfigureAwait(false);
			try
			{
				var handled = await adaptor.AdaptAsync(adaptorContext);
				if (handled != null)
				{
					bindingContext.Result = ModelBindingResult.Success(new ChannelProviderDeliveryReportRequest(handled));
					return;
				}
			}
			catch (DeliveryReportRequestValidationException ex)
			{
				// another registration of the same provider (e.g. a second account) may own this request.
				lastValidationRejection = ex;
			}
			catch
			{
				//Eat any unexpected adaptor exceptions
			}
		}

		if (lastValidationRejection != null)
			bindingContext.ModelState.TryAddModelException(bindingContext.ModelName, lastValidationRejection);

		bindingContext.Result = ModelBindingResult.Failed();
	}
}
