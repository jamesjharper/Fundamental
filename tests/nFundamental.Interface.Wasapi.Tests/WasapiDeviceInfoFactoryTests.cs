﻿using System;
using Fundamental.Interface.Wasapi;
using Fundamental.Interface.Wasapi.Internal;
using Fundamental.Interface.Wasapi.Interop;
using NSubstitute;
using NUnit.Framework;

namespace nFundamental.Interface.Wasapi.Tests
{

    [TestFixture]
    public class WasapiDeviceInfoFactoryTests
    {
        private IWasapiPropertyNameTranslator WasapiPropertyNameTranslatorTestFixture { get; set; }

        private IWasapiInterfaceNotifyClient WasapiInterfaceNotifyClient { get; set; }

        [SetUp]
        public void SetUp()
        {
            WasapiInterfaceNotifyClient = Substitute.For<IWasapiInterfaceNotifyClient>();
            WasapiPropertyNameTranslatorTestFixture = Substitute.For<IWasapiPropertyNameTranslator>();
        }

        private WasapiDeviceInfoFactory GetTestFixture() 
            => new WasapiDeviceInfoFactory(WasapiInterfaceNotifyClient, WasapiPropertyNameTranslatorTestFixture);



        [Test]
        public void CanGetDeviceInfo()
        {
            // -> ARRANGE:
            var fixture = GetTestFixture();
            var expectedDeviceId = "115977BC-AB6A-4892-9CD6-CE2C6B287109";

            var expectedMmDevice = Substitute.For<IMMDevice>();
            var inputToken = new WasapiDeviceToken(expectedDeviceId, expectedMmDevice);

            // -> ACT
            var deviceInfo = fixture.GetInfoDevice(inputToken);

            // -> ASSERT
            Assert.AreEqual(expectedDeviceId, deviceInfo.DeviceToken.Id);
            Assert.AreEqual(expectedMmDevice, deviceInfo.DeviceToken.MmDevice);
        }
    }
}