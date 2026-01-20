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

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Transmitly.ChannelProvider.Configuration;
using Transmitly.Delivery;
using System.Linq;
using System.Collections.Generic;

namespace Transmitly.Microsoft.Aspnet.Mvc;

class ChannelProviderDeliveryReportRequestModelBinder : IModelBinder
{
	private readonly List<Lazy<IChannelProviderDeliveryReportRequestAdaptor>> _adaptorInstances;

	public ChannelProviderDeliveryReportRequestModelBinder(IChannelProviderFactory adaptor)
	{
		var adaptors = AsyncHelper.RunSync(adaptor.GetAllDeliveryReportRequestAdaptorsAsync);
		_adaptorInstances = adaptors.Select(s => new Lazy<IChannelProviderDeliveryReportRequestAdaptor>(AsyncHelper.RunSync(() => adaptor.ResolveDeliveryReportRequestAdaptorAsync(s)))).ToList();
	}

	public async Task BindModelAsync(ModelBindingContext bindingContext)
	{
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

		var adaptorContext = new DefaultRequestAdaptorContext(request, content);

		foreach (var adaptor in _adaptorInstances)
		{
			try
			{
				var handled = await adaptor.Value.AdaptAsync(adaptorContext);
				if (handled != null)
				{
					bindingContext.Result = ModelBindingResult.Success(new ChannelProviderDeliveryReportRequest(handled));
					return;
				}
			}
			catch
			{
				//Eat any unexpected adaptor exceptions
			}
		}
		bindingContext.Result = ModelBindingResult.Failed();
	}
}
