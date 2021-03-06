﻿using System;
using System.Linq;

namespace Fundamental.Interface.Wasapi.Extentions
{
    public static class DeviceStateExtentions
    {
        /// <summary>
        /// Converts DeviceState enum to WASAPI DeviceState enum.
        /// </summary>
        /// <param name="deviceState">State of the device.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentOutOfRangeException">deviceState - null</exception>
        public static Interop.DeviceState ConvertToWasapiDeviceState(this DeviceState deviceState)
        {
            switch (deviceState)
            {
                case DeviceState.Available:
                    return Interop.DeviceState.Active;

                case DeviceState.Disabled:
                    return Interop.DeviceState.Disabled;

                case DeviceState.NotPresent:
                    return Interop.DeviceState.NotPresent;

                case DeviceState.Unplugged:
                    return Interop.DeviceState.Unplugged;

                default:
                    throw new ArgumentOutOfRangeException(nameof(deviceState), deviceState, null);
            }
        }

        /// <summary>
        /// Converts the state of to WASAPI device.
        /// </summary>
        /// <param name="stateMask">The state mask.</param>
        /// <returns></returns>
        public static Interop.DeviceState ConvertToWasapiDeviceState(this DeviceState[] stateMask)
        {
            return stateMask.Select(x => x.ConvertToWasapiDeviceState())
                            .Aggregate((a, b) => a | b); // Or the list together to create flagged enum
        }


        /// <summary>
        /// Converts a WASAPI state of to fundamental device state.
        /// </summary>
        /// <param name="deviceState">State of the device.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentOutOfRangeException">deviceState - null</exception>
        public static DeviceState ConvertToFundamentalDeviceState(this Interop.DeviceState deviceState)
        {
            if ((deviceState & Interop.DeviceState.Active) != 0)
                return DeviceState.Available;

            if ((deviceState & Interop.DeviceState.Disabled) != 0)
                return DeviceState.Disabled;

            if ((deviceState & Interop.DeviceState.NotPresent) != 0)
                return DeviceState.NotPresent;

            if ((deviceState & Interop.DeviceState.Unplugged) != 0)
                return DeviceState.Unplugged;

            throw new ArgumentOutOfRangeException(nameof(deviceState), deviceState, null);
        }
    }
}
