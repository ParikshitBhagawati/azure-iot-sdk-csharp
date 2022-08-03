﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Common.Exceptions;
using Microsoft.Azure.Devices.E2ETests.Helpers;
using Microsoft.Rest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClientOptions = Microsoft.Azure.Devices.Client.IotHubClientOptions;

namespace Microsoft.Azure.Devices.E2ETests.IotHub.Service
{
    /// <summary>
    /// Tests to ensure authentication using Azure active directory succeeds in all the clients.
    /// </summary>
    [TestClass]
    [TestCategory("E2E")]
    [TestCategory("IoTHub")]
    public class TokenCredentialAuthenticationTests : E2EMsTestBase
    {
        private readonly string _devicePrefix = $"{nameof(TokenCredentialAuthenticationTests)}_";

        [LoggedTestMethod]
        public async Task DevicesClient_Http_TokenCredentialAuth_Success()
        {
            // arrange
            using var serviceClient = new IotHubServiceClient(
                TestConfiguration.IoTHub.GetIotHubHostName(),
                TestConfiguration.IoTHub.GetClientSecretCredential());

            var device = new Device(Guid.NewGuid().ToString());

            // act
            Device createdDevice = await serviceClient.Devices.CreateAsync(device).ConfigureAwait(false);

            // assert
            Assert.IsNotNull(createdDevice);

            // cleanup
            await serviceClient.Devices.DeleteAsync(device.Id).ConfigureAwait(false);
        }

        [LoggedTestMethod]
        public async Task JobClient_Http_TokenCredentialAuth_Success()
        {
            // arrange
            using var serviceClient = new IotHubServiceClient(TestConfiguration.IoTHub.GetIotHubHostName(), TestConfiguration.IoTHub.GetClientSecretCredential());

            string jobId = "JOBSAMPLE" + Guid.NewGuid().ToString();
            string jobDeviceId = "JobsSample_Device";
            string query = $"DeviceId IN ['{jobDeviceId}']";
            var twin = new Twin(jobDeviceId);

            try
            {
                // act
                ScheduledTwinUpdate twinUpdate = new ScheduledTwinUpdate
                {
                    Twin = twin,
                    QueryCondition = query,
                    StartTimeUtc = DateTime.UtcNow
                };
                ScheduledJobsOptions twinUpdateOptions = new ScheduledJobsOptions
                {
                    JobId = jobId,
                    MaxExecutionTimeInSeconds = (long)TimeSpan.FromMinutes(2).TotalSeconds
                };
                JobResponse createJobResponse = await serviceClient.ScheduledJobs
                    .ScheduleTwinUpdateAsync(
                        twinUpdate,
                        twinUpdateOptions)
                    .ConfigureAwait(false);
            }
            catch (ThrottlingException)
            {
                // Concurrent jobs can be rejected, but it still means authentication was successful. Ignore the exception.
            }
        }

        [LoggedTestMethod]
        public async Task DigitalTwinClient_Http_TokenCredentialAuth_Success()
        {
            // arrange
            TestDevice testDevice = await TestDevice.GetTestDeviceAsync(Logger, _devicePrefix).ConfigureAwait(false);
            string thermostatModelId = "dtmi:com:example:TemperatureController;1";

            // Create a device client instance initializing it with the "Thermostat" model.
            var options = new ClientOptions(new IotHubClientMqttSettings())
            {
                ModelId = thermostatModelId,
            };
            using IotHubDeviceClient deviceClient = testDevice.CreateDeviceClient(options);

            // Call openAsync() to open the device's connection, so that the ModelId is sent over Mqtt CONNECT packet.
            await deviceClient.OpenAsync().ConfigureAwait(false);

            using var serviceClient = new IotHubServiceClient(
                TestConfiguration.IoTHub.GetIotHubHostName(),
                TestConfiguration.IoTHub.GetClientSecretCredential());

            // act
            DigitalTwinGetResponse<ThermostatTwin> response = await serviceClient.DigitalTwins
                .GetAsync<ThermostatTwin>(testDevice.Id)
                .ConfigureAwait(false);

            ThermostatTwin twin = response.DigitalTwin;

            // assert
            twin.Metadata.ModelId.Should().Be(thermostatModelId);

            // cleanup
            await testDevice.RemoveDeviceAsync().ConfigureAwait(false);
        }

        [LoggedTestMethod]
        public async Task Service_Amqp_TokenCredentialAuth_Success()
        {
            // arrange
            string ghostDevice = $"{nameof(Service_Amqp_TokenCredentialAuth_Success)}_{Guid.NewGuid()}";
            using var serviceClient = ServiceClient.Create(
                TestConfiguration.IoTHub.GetIotHubHostName(),
                TestConfiguration.IoTHub.GetClientSecretCredential(),
                TransportType.Amqp);
            await serviceClient.OpenAsync().ConfigureAwait(false);
            using var message = new Message(Encoding.ASCII.GetBytes("Hello, Cloud!"));

            // act
            Func<Task> act = async () => await serviceClient.SendAsync(ghostDevice, message).ConfigureAwait(false);

            // assert
            await act.Should().ThrowAsync<DeviceNotFoundException>();
        }
    }
}
