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

using Transmitly.Microsoft.Aspnet.Mvc;
using Microsoft.AspNetCore.Mvc;

namespace Transmitly;

/// <summary>
/// Extension methods related to Aspnet Mvc projects.
/// </summary>
public static class TransmitlyAspNetMvcExtensions
{
	/// <summary>
	/// Adds the transmitly channel provider delivery report model binder.
	/// </summary>
	public static void AddTransmitlyDeliveryReportModelBinders(this MvcOptions options)
	{
		options.ModelBinderProviders.Insert(0, new ChannelProviderModelBinderProvider());
	}
}
