﻿// ﻿﻿Copyright (c) Code Impressions, LLC. All Rights Reserved.
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

using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
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

		foreach (var adaptor in _adaptorInstances)
		{
			try
			{
				var handled = await adaptor.Value.AdaptAsync(new DefaultRequestAdaptorContext(bindingContext.HttpContext.Request));
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
