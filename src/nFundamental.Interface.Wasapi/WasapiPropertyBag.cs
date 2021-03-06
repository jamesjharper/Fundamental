﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fundamental.Core;
using Fundamental.Interface.Wasapi.Internal;
using Fundamental.Interface.Wasapi.Interop;
using Fundamental.Interface.Wasapi.Win32;
using Fundamental.Wave.Format;

namespace Fundamental.Interface.Wasapi
{
    public class WasapiPropertyBag : IPropertyBag
    {
        /// <summary>
        /// The WASAPI property name translator
        /// </summary>
        private readonly IWasapiPropertyNameTranslator _wasapiPropertyNameTranslator;

        /// <summary>
        /// The special converters used for known value types
        /// </summary>
        private readonly Dictionary<PropertyKey, Func<object, object>> _specialConverters;


        /// <summary>
        /// The wave format converter used for converting WAVEFORMATEX objects in to readable structures
        /// </summary>
        private readonly IAudioFormatConverter<WaveFormat> _waveFormatConverter;

        /// <summary>
        /// The underlying property store
        /// </summary>
        public IPropertyStore PropertyStore { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="WasapiPropertyBag" /> class.
        /// </summary>
        /// <param name="propertyStore">The property store.</param>
        /// <param name="wasapiPropertyNameTranslator">The WASAPI property name translator.</param>
        /// <param name="waveFormatConverter"></param>
        public WasapiPropertyBag(IPropertyStore propertyStore,
                                 IWasapiPropertyNameTranslator wasapiPropertyNameTranslator,
                                 IAudioFormatConverter<WaveFormat> waveFormatConverter)
        {
            _wasapiPropertyNameTranslator = wasapiPropertyNameTranslator;
            _waveFormatConverter = waveFormatConverter;
            PropertyStore = propertyStore;

            _specialConverters = new Dictionary<PropertyKey, Func<object, object>>
            {
                [PropertyKeys.AudioEngineOemFormat]    = ConvertBytesToAudioFormat,
                [PropertyKeys.AudioEngineDeviceFormat] = ConvertBytesToAudioFormat
            };


        }

     

        /// <summary>
        /// Gets the <see cref="System.Object"/> with the specified key.
        /// </summary>
        /// <value>
        /// The <see cref="System.Object"/>.
        /// </value>
        /// <param name="keyId">The key Id.</param>
        /// <returns></returns>
        public object this[string keyId]
        {
            get
            {
                object result;
                return TryGetValue(keyId, out result) ? result : null;
            }
        }

        /// <summary>
        /// Determines whether the <see cref="T:System.Collections.Generic.IDictionary`2" /> contains an element with the specified key.
        /// </summary>
        /// <param name="keyId">The key to locate in the <see cref="T:System.Collections.Generic.IDictionary`2" />.</param>
        /// <returns>
        /// true if the <see cref="T:System.Collections.Generic.IDictionary`2" /> contains an element with the key; otherwise, false.
        /// </returns>
        public bool ContainsKey(string keyId)
        {
            object dummy;
            return TryGetValue(keyId, out dummy);
        }

        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        /// <param name="keyId">The key Id whose value to get.</param>
        /// <param name="value">When this method returns, the value associated with the specified key, if the key is found; otherwise, the default value for the type of the <paramref name="value" /> parameter. This parameter is passed uninitialized.</param>
        /// <returns>
        /// true if the object that implements <see cref="T:System.Collections.Generic.IDictionary`2" /> contains an element with the specified key; otherwise, false.
        /// </returns>
        public bool TryGetValue(string keyId, out object value)
        {
            value = null;
           
            // If the key was not recognized, an empty GUID should be returned.
            var propertyKey = _wasapiPropertyNameTranslator.ResolvePropertyKey(keyId);
            if (Equals(propertyKey.FormatId, Guid.Empty))
                return false;

            // If the key isn't found in the property bag then EMPTY is returned
            PropVariant variant;
            PropertyStore.GetValue(propertyKey, out variant).ThrowIfFailed();
            if (variant.ValueType == VariantType.VT_EMPTY)
                return false;

            // If we don't know how to read the returned 
            // variant type, we ignore it
            if (!variant.IsVariantTypeSupported())
                return false;
            

            value = ToValue(variant, propertyKey);
            return true;
        }


        /// <summary>
        /// Gets an <see cref="T:System.Collections.Generic.ICollection`1" /> containing the keys of the <see cref="T:System.Collections.Generic.IDictionary`2" />.
        /// </summary>
        public IEnumerable<IPropertyBagKey> Keys
        {
            get { return this.Select(x => x.Key); }
        }

        /// <summary>
        /// Gets an <see cref="T:System.Collections.Generic.ICollection`1" /> containing the values in the <see cref="T:System.Collections.Generic.IDictionary`2" />.
        /// </summary>
        public IEnumerable<object> Values
        {
            get { return this.Select(x => x.Value); }
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>
        /// An enumerator that can be used to iterate through the collection.
        /// </returns>
        public IEnumerator<KeyValuePair<IPropertyBagKey, object>> GetEnumerator()
        {
            foreach (var keyValuePair in GetPropertyKeyValueEnumerable())
            {
                var key = _wasapiPropertyNameTranslator.ResolvePropertyKey(keyValuePair.Key);

                // Filter out properties who's names we couldn't resolve
                if(Equals(key, null))
                    continue;

                var value = ToValue(keyValuePair.Value, keyValuePair.Key);
                yield return new KeyValuePair<IPropertyBagKey, object>(key, value);
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the collection.
        /// </returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        // Private Methods

        private IEnumerable<PropertyKey> GetPropertyKeyEnumerable()
        {
            int count;
            PropertyStore.GetCount(out count);

            for (var i = 0; i < count; i++)
            {
                PropertyKey propertyKey;
                PropertyStore.GetAt(i, out propertyKey).ThrowIfFailed();

                yield return propertyKey;
            }
        }

        private IEnumerable<KeyValuePair<PropertyKey, PropVariant>> GetPropertyKeyValueEnumerable()
        {
            foreach (var propertyKey in GetPropertyKeyEnumerable())
            {
                PropVariant variant;
                PropertyStore.GetValue(propertyKey, out variant).ThrowIfFailed();

                if(!variant.IsVariantTypeSupported())
                    continue;

                yield return new KeyValuePair<PropertyKey, PropVariant>(propertyKey, variant);
            }
        }

        // Type Conversion methods 


        private object ToValue(PropVariant value, PropertyKey key)
        {
            Func<object, object> convertFunc;
            if (!_specialConverters.TryGetValue(key, out convertFunc))
            {
                return value.ToObject();
            }

            try
            {
                return convertFunc(value.ToObject());
            }
            catch (Exception)
            {
                // If we fail to convert, then we return the original value
                return value.ToObject();
            }
        }

        private object ConvertBytesToAudioFormat(object bytes)
        {
            var waveFormatEx = WaveFormat.FromBytes((byte[])bytes);
            return _waveFormatConverter.Convert(waveFormatEx);
        }
    }
}
