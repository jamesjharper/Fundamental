﻿using System;
using Fundamental.Core;
using Fundamental.Core.AudioFormats;
using Fundamental.Core.Memory;
using Fundamental.Interface.Wasapi.Interop;
using Fundamental.Interface.Wasapi.Win32;
using Fundamental.Wave.Format;

namespace Fundamental.Interface.Wasapi.Internal
{
    /// <summary>
    /// An Anti corruption layer for the IAudioClient COM object.
    /// Interaction with IAudioClient can require pointer Manipulation, which is a little ugly in managed languages
    /// </summary>
    public class WasapiAudioClientInterop : IWasapiAudioClientInterop
    {
        /// <summary>
        /// The thread dispatcher
        /// </summary>
        private readonly IComThreadInteropStrategy _comThreadInteropStrategy;

        /// <summary>
        /// The wave format converter
        /// </summary>
        private readonly IAudioFormatConverter<WaveFormat> _waveFormatConverter;


        /// <summary>
        /// The initialized wave format
        /// </summary>
        private WaveFormat _initializedWavFormat;

        /// <summary>
        /// Gets the COM instance.
        /// </summary>
        /// <value>
        /// The COM instance.
        /// </value>
        public IAudioClient ComInstance { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="WasapiAudioClientInterop"/> class.
        /// </summary>
        /// <param name="audioClient">The audio client.</param>
        /// <param name="comThreadInteropStrategy"></param>
        /// <param name="waveFormatConverter">The wave format converter.</param>
        public WasapiAudioClientInterop(IAudioClient audioClient,
                                        IComThreadInteropStrategy comThreadInteropStrategy,
                                        IAudioFormatConverter<WaveFormat> waveFormatConverter)
        {
            ComInstance = audioClient;
            _comThreadInteropStrategy = comThreadInteropStrategy;
            _waveFormatConverter = waveFormatConverter;
        }

        /// <summary>
        /// Gets the number of audio frames that the buffer can hold.
        /// </summary>
        /// <returns></returns>
        public int GetBufferSize()
        {
            uint outInt;
             ComInstance.GetBufferSize(out outInt).ThrowIfFailed();
            return checked((int)outInt);
        }


        /// <summary>
        /// Gets the maximum latency for the current stream and can be called any time after the stream has been initialized.
        /// </summary>
        /// <returns></returns>
        public TimeSpan GetStreamLatency()
        {
            ulong outuLong;
            ComInstance.GetStreamLatency(out outuLong).ThrowIfFailed();
            return TimeSpan.FromTicks(checked((long)outuLong));
        }

        /// <summary>
        /// Gets the number of frames of padding in the endpoint buffer.
        /// </summary>
        /// <returns></returns>
        public int GetCurrentPadding()
        {
            uint outInt;
            ComInstance.GetCurrentPadding(out outInt).ThrowIfFailed();
            return checked((int)outInt);
        }

        /// <summary>
        /// The IsFormatSupported method indicates whether the audio endpoint device supports a particular stream format
        /// </summary>
        /// <param name="shareMode">The share mode.</param>
        /// <param name="format">The format.</param>
        /// <param name="closestMatch">The closest match.</param>
        /// <returns>
        ///   <c>true</c> if [is format supported] [the specified share mode]; otherwise, <c>false</c>.
        /// </returns>
        public bool IsFormatSupported(AudioClientShareMode shareMode, IAudioFormat format, out IAudioFormat closestMatch)
        {
            var waveFormatEx = _waveFormatConverter.Convert(format);

            using (var pWaveFormatEx = CoTaskMemPtr.Alloc(waveFormatEx.ByteSize))
            {
                waveFormatEx.Write(pWaveFormatEx);

                // ReSharper disable once RedundantAssignment
                IntPtr pClosestMatchOut;
                var hresult = ComInstance.IsFormatSupported(shareMode, pWaveFormatEx, out pClosestMatchOut);

                // Try reading the closest match from the pointer to the pointer
                closestMatch = ReadAudioFormat(pClosestMatchOut);

                // Succeeded with a closest match to the specified format.
                if (hresult == HResult.E_FAIL)
                    return false;

                //  Succeeded but the specified format is not supported in exclusive mode.
                if (hresult == HResult.AUDCLNT_E_UNSUPPORTED_FORMAT)
                    return shareMode != AudioClientShareMode.Exclusive;


                hresult.ThrowIfFailed();
                return hresult == HResult.S_OK;
            }
        }


        /// <summary>
        /// The GetMixFormat method retrieves the stream format that the audio engine uses for its internal processing of shared-mode streams.
        /// </summary>
        public IAudioFormat GetMixFormat()
        {
            IntPtr format;
            ComInstance.GetMixFormat(out format).ThrowIfFailed();
            return ReadAudioFormat(format);
        }


        /// <summary>
        /// The GetMixFormat method retrieves the stream format that the audio engine uses for its internal processing of shared-mode streams.
        /// </summary>
        /// <returns></returns>
        public DevicePeriod GetDevicePeriod()
        {
            ulong phnsDefaultDevicePeriod;
            ulong phnsMinimumDevicePeriod;

            ComInstance.GetDevicePeriod(out phnsDefaultDevicePeriod, out phnsMinimumDevicePeriod).ThrowIfFailed();

            return new DevicePeriod
            (
                TimeSpan.FromTicks(checked((long)phnsDefaultDevicePeriod)),
                TimeSpan.FromTicks(checked((long)phnsDefaultDevicePeriod))
            );
        }


        /// <summary>
        /// Initializes the audio stream.
        /// </summary>
        /// <param name="shareMode">The share mode.</param>
        /// <param name="streamFlags">The stream flags.</param>
        /// <param name="bufferDuration">Duration of the buffer.</param>
        /// <param name="devicePeriod">The device period.</param>
        /// <param name="format">The format.</param>
        public void Initialize(AudioClientShareMode shareMode,
                               AudioClientStreamFlags streamFlags,
                               TimeSpan bufferDuration,
                               TimeSpan devicePeriod,
                               IAudioFormat format)
        {
            Initialize(shareMode, streamFlags, bufferDuration, devicePeriod, format, Guid.Empty); 
        }

        /// <summary>
        /// Initializes the audio stream.
        /// </summary>
        /// <param name="shareMode">The share mode.</param>
        /// <param name="streamFlags">The stream flags.</param>
        /// <param name="bufferDuration">Duration of the buffer.</param>
        /// <param name="devicePeriod">The device period.</param>
        /// <param name="format">The format.</param>
        /// <param name="sessionId">The session identifier.</param>
        public void Initialize(AudioClientShareMode shareMode,
                               AudioClientStreamFlags streamFlags,
                               TimeSpan bufferDuration,
                               TimeSpan devicePeriod,
                               IAudioFormat format,
                               Guid sessionId)
        {

            var waveFormatEx = _waveFormatConverter.Convert(format);
            Initialize(shareMode, streamFlags, bufferDuration, devicePeriod, waveFormatEx, sessionId);
        }

        /// <summary>
        /// Initializes the specified share mode.
        /// </summary>
        /// <param name="shareMode">The share mode.</param>
        /// <param name="streamFlags">The stream flags.</param>
        /// <param name="bufferDuration">Duration of the buffer.</param>
        /// <param name="devicePeriod">The device period.</param>
        /// <param name="format">The format.</param>
        /// <param name="sessionId">The session identifier.</param>
        private void Initialize(AudioClientShareMode shareMode,
                                AudioClientStreamFlags streamFlags,
                                TimeSpan bufferDuration,
                                TimeSpan devicePeriod,
                                WaveFormat format,
                                Guid sessionId)
        {
            // Check to see if we need to jump threads
            if (_comThreadInteropStrategy.RequiresInvoke())
            {
                _comThreadInteropStrategy.InvokeOnTargetThread
                    (
                        new Action<AudioClientShareMode, AudioClientStreamFlags, TimeSpan, TimeSpan, WaveFormatEx, Guid>(Initialize),
                        shareMode, streamFlags, bufferDuration, devicePeriod, format, sessionId
                    );
                return;
            }

            var bufferDurationTicks = checked((uint)bufferDuration.Ticks);
            var devicePeriodTicks = checked((uint)devicePeriod.Ticks);

            using (var pWaveFormatEx = CoTaskMemPtr.Alloc(format.ByteSize))
            {
                format.Write(pWaveFormatEx);
                ComInstance.Initialize(shareMode, streamFlags, bufferDurationTicks, devicePeriodTicks, pWaveFormatEx, sessionId).ThrowIfFailed();
                _initializedWavFormat = format;
            }
        }


        /// <summary>
        /// Starts the audio stream.
        /// </summary>
        public void Start()
        {
            // Check to see if we need to jump threads
            if (_comThreadInteropStrategy.RequiresInvoke())
            {
                _comThreadInteropStrategy.InvokeOnTargetThread(new Action(Start));
                return;
            }

            ComInstance.Start().ThrowIfFailed();
        }

        /// <summary>
        /// Stops the audio stream.
        /// </summary>
        public void Stop()
        {
            // Check to see if we need to jump threads
            if (_comThreadInteropStrategy.RequiresInvoke())
            {
                _comThreadInteropStrategy.InvokeOnTargetThread(new Action(Stop));
                return;
            }

            ComInstance.Stop().ThrowIfFailed();
        }


        /// <summary>
        /// Resets the audio stream.
        /// </summary>
        public void Reset()
        {
            // Check to see if we need to jump threads
            if (_comThreadInteropStrategy.RequiresInvoke())
            {
                _comThreadInteropStrategy.InvokeOnTargetThread(new Action(Reset));
                return;
            }

            ComInstance.Reset().ThrowIfFailed();
    
        }


        /// <summary>
        /// Sets the event handle.
        /// </summary>
        /// <param name="handle">The handle.</param>
        public void SetEventHandle(IntPtr handle)
        {
            ComInstance.SetEventHandle(handle).ThrowIfFailed();
        }

        /// <summary>
        /// Gets the capture client.
        /// </summary>
        /// <returns></returns>
        public IWasapiAudioCaptureClientInterop GetCaptureClient()
        {
            if (_initializedWavFormat == null)
                throw new DeviceNotInitializedException("Unable to get a capture client as device is not currently initialized.");

            var audioCaptureClientComInstance = GetService<IAudioCaptureClient>(IIds.IAudioCaptureClientGuid);

            var blockAlignment = _initializedWavFormat.BlockAlign;
            return new WasapiAudioCaptureClientInterop(audioCaptureClientComInstance, blockAlignment);
        }

        /// <summary>
        /// Gets the render client.
        /// </summary>
        /// <returns></returns>
        public IWasapiAudioRenderClientInterop GetRenderClient()
        {
            if (_initializedWavFormat == null)
                throw new DeviceNotInitializedException("Unable to get a capture client as device is not currently initialized.");

            var audioRenderClientComInstance = GetService<IAudioRenderClient>(IIds.IAudioRenderClientGuid);

            var blockAlignment = _initializedWavFormat.BlockAlign;
            return new WasapiAudioRenderClientInterop(audioRenderClientComInstance, ComInstance, blockAlignment);
        }


        /// <summary>
        /// Gets the service.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetService<T>(Guid iid) where T : class
        {
            var result = GetService(iid);
            return ComObject.QuearyInterface<T>(result);
        }

        /// <summary>
        /// Gets the service.
        /// </summary>
        /// <param name="interfaceId">The interface identifier.</param>
        /// <returns></returns>
        public object GetService(Guid interfaceId)
        {
            // Check to see if we need to jump threads
            if (_comThreadInteropStrategy.RequiresInvoke())
                return _comThreadInteropStrategy.InvokeOnTargetThread(new Func<Guid,object>(GetService), interfaceId);


            object outObject;
            ComInstance.GetService(interfaceId, out outObject).ThrowIfFailed();
            return outObject;
      
        }

        // Private Methods

        private IAudioFormat ReadAudioFormat(IntPtr pointer)
        {
            using (var safePointer = CoTaskMemPtr.Attach(pointer))
            {

                if (safePointer == NativePtr.Null)
                    return null;
                var formatEx = WaveFormatEx.FromPointer(safePointer.Ptr);
                return _waveFormatConverter.Convert(formatEx);
            }
        }
    }
}
