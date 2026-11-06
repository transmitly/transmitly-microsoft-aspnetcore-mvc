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

using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Transmitly.ChannelProvider;
using Transmitly.ChannelProvider.Configuration;
using Transmitly.Delivery;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace Transmitly.Microsoft.AspnetCore.Mvc.Tests
{
	/// <summary>
	/// Runs delivery-report requests through the real MVC pipeline: model binder, adaptor context and base controller.
	/// </summary>
	[TestClass]
	public sealed class DeliveryReportEndpointTests
	{
		private const string Url = "/delivery?tlyc=sms&tlycp=test";

		[TestMethod]
		public async Task AcceptedRequest_Returns200AndDispatchesReports()
		{
			var (client, communications) = await StartAsync(Accept());

			var response = await client.PostAsync(Url, Json("{}"));

			Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
			communications.Verify(c => c.DispatchAsync(It.Is<IReadOnlyCollection<DeliveryReport>>(r => r.Count == 1)), Times.Once);
		}

		[TestMethod]
		public async Task FailedValidation_Returns403AndDoesNotDispatch()
		{
			var (client, communications) = await StartAsync(Reject());

			var response = await client.PostAsync(Url, Json("{}"));

			Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
			communications.Verify(c => c.DispatchAsync(It.IsAny<IReadOnlyCollection<DeliveryReport>>()), Times.Never);
		}

		[TestMethod]
		public async Task RejectedByOneRegistration_AcceptedByAnother_Returns200()
		{
			// e.g. two accounts of the same provider: the first adaptor's secret doesn't match, the second's does.
			var (client, _) = await StartAsync(Reject(), Accept());

			var response = await client.PostAsync(Url, Json("{}"));

			Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
		}

		[TestMethod]
		public async Task NotAddressedToAnyAdaptor_IsNotReportedAsForbidden()
		{
			var (client, _) = await StartAsync(new FakeAdaptor(_ => null));

			var response = await client.PostAsync(Url, Json("{}"));

			Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
		}

		[TestMethod]
		public async Task FormPost_AdaptorSeesEveryFieldAndTheRequestUrl()
		{
			// MVC's form value provider reads the body before binding; form-signing providers (Twilio) still need every field.
			string? content = null, requestUrl = null, formValue = null;
			var (client, _) = await StartAsync(new FakeAdaptor(ctx =>
			{
				content = ctx.Content;
				requestUrl = ctx.RequestUrl;
				formValue = ctx.GetFormValue("MessageStatus");
				return [Report()];
			}));

			await client.PostAsync(Url, new FormUrlEncodedContent(new Dictionary<string, string> { ["MessageSid"] = "SM1", ["MessageStatus"] = "delivered now" }));

			var fields = QueryHelpers.ParseQuery(content);
			Assert.AreEqual("SM1", fields["MessageSid"].ToString());
			Assert.AreEqual("delivered now", fields["MessageStatus"].ToString());
			Assert.AreEqual("delivered now", formValue);
			Assert.AreEqual("http://localhost" + Url, requestUrl);
		}

		[TestMethod]
		public async Task JsonPost_AdaptorSeesRawBodyAndHeaders()
		{
			const string body = "[{\"event\":\"delivered\"}]";
			string? content = null, header = null;
			var (client, _) = await StartAsync(new FakeAdaptor(ctx =>
			{
				content = ctx.Content;
				header = ctx.GetHeaderValue("X-Test-Signature");
				return [Report()];
			}));

			var request = new HttpRequestMessage(HttpMethod.Post, Url) { Content = Json(body) };
			request.Headers.Add("X-Test-Signature", "sig");
			await client.SendAsync(request);

			Assert.AreEqual(body, content);
			Assert.AreEqual("sig", header);
		}

		[TestMethod]
		public async Task PipelineContext_ComesFromQueryOnly()
		{
			// Unsigned headers must not be able to supply pipeline context.
			string? fromQuery = null, fromHeader = "unset";
			var (client, _) = await StartAsync(new FakeAdaptor(ctx =>
			{
				if (ctx.GetQueryValue(DeliveryUtil.PipelineIntentKey) != null)
					fromQuery = ctx.PipelineIntent;
				else
					fromHeader = ctx.PipelineIntent;
				return [Report()];
			}));

			await client.PostAsync($"{Url}&{DeliveryUtil.PipelineIntentKey}=order-created", Json("{}"));
			var injected = new HttpRequestMessage(HttpMethod.Post, Url) { Content = Json("{}") };
			injected.Headers.Add(DeliveryUtil.PipelineIntentKey, "injected");
			await client.SendAsync(injected);

			Assert.AreEqual("order-created", fromQuery);
			Assert.IsNull(fromHeader);
		}

		private static async Task<(HttpClient Client, Mock<ICommunicationsClient> Communications)> StartAsync(params IChannelProviderDeliveryReportRequestAdaptor[] adaptors)
		{
			var communications = new Mock<ICommunicationsClient>();
			var builder = WebApplication.CreateBuilder();
			builder.WebHost.UseTestServer();
			builder.Services.AddSingleton<IChannelProviderFactory>(new FakeChannelProviderFactory(adaptors));
			builder.Services.AddSingleton(communications.Object);
			builder.Services.AddControllers(o => o.AddTransmitlyDeliveryReportModelBinders()).AddApplicationPart(typeof(DeliveryController).Assembly);
			var app = builder.Build();
			app.MapControllers();
			await app.StartAsync();
			return (app.GetTestClient(), communications);
		}

		private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

		private static DeliveryReport Report() =>
			new(DeliveryReport.Event.StatusChanged(), "sms", "test", null, null, "id-1", CommunicationsStatus.Success("test", "Delivered"), null, null, null);

		private static FakeAdaptor Accept() => new(_ => [Report()]);

		private static FakeAdaptor Reject() => new(_ => throw new DeliveryReportRequestValidationException("signature is invalid"));

		private sealed class FakeAdaptor(Func<IRequestAdaptorContext, IReadOnlyCollection<DeliveryReport>?> adapt) : IChannelProviderDeliveryReportRequestAdaptor
		{
			public Task<IReadOnlyCollection<DeliveryReport>?> AdaptAsync(IRequestAdaptorContext adaptorContext) => Task.FromResult(adapt(adaptorContext));
		}

		private sealed class FakeChannelProviderFactory(IChannelProviderDeliveryReportRequestAdaptor[] adaptors) : IChannelProviderFactory
		{
			private sealed record Registration(int Index) : IDeliveryReportRequestAdaptorRegistration
			{
				public Type Type => typeof(FakeAdaptor);
				public object? Configuration => null;
			}

			public Task<IReadOnlyCollection<IDeliveryReportRequestAdaptorRegistration>> GetAllDeliveryReportRequestAdaptorsAsync() =>
				Task.FromResult<IReadOnlyCollection<IDeliveryReportRequestAdaptorRegistration>>(adaptors.Select((_, i) => new Registration(i)).ToArray());

			public Task<IChannelProviderDeliveryReportRequestAdaptor> ResolveDeliveryReportRequestAdaptorAsync(IDeliveryReportRequestAdaptorRegistration registration) =>
				Task.FromResult(adaptors[((Registration)registration).Index]);

			public Task<IReadOnlyCollection<IChannelProviderRegistration>> GetAllAsync() => throw new NotSupportedException();

			public Task<IChannelProviderDispatcher?> ResolveDispatcherAsync(IChannelProviderRegistration channelProvider, IChannelProviderDispatcherRegistration channelProviderDispatcherRegistration) => throw new NotSupportedException();
		}
	}

	[Route("delivery")]
	public sealed class DeliveryController(ICommunicationsClient communicationsClient) : ChannelProviderDeliveryReportController(communicationsClient);
}
