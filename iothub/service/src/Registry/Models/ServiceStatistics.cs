﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Newtonsoft.Json;

namespace Microsoft.Azure.Devices
{
    /// <summary>
    /// Contains service statistics that can be retrieved from IoT hub.
    /// </summary>
    public class ServiceStatistics
    {
        /// <summary>
        /// Number of devices connected to IoT hub.
        /// </summary>
        [JsonProperty(PropertyName = "connectedDeviceCount")]
        public long ConnectedDeviceCount { get; set; }
    }
}
