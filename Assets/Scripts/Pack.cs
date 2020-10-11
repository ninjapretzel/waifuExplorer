#if UNITY_2017 || UNITY_2018 || UNITY_2019 || UNITY_2020
#define UNITY
using UnityEngine;
#else

#endif

using System;
using System.Runtime.CompilerServices;

/// <summary> Class holding code for packing structs </summary>
public static class Pack {

	/// <summary> Pack a struct into a Base64 <see cref="string"/> </summary>
	/// <typeparam name="T"> Generic type of parameter </typeparam>
	/// <param name="value"> Parameter to pack </param>
	/// <returns> Base64 <see cref="string"/> from converting struct into <see cref="byte[]"/>, then encoding </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string Base64<T>(T value) where T : struct {
		byte[] copy = Unsafe.ToBytes(value);
		return Convert.ToBase64String(copy);
	}

	/// <summary> Change <see cref="byte[]"/> into Base64 <see cref="string"/> </summary>
	/// <param name="data"> data to pack </param>
	/// <returns> packed <see cref="string "/></returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string Base64(byte[] data) {
		return Convert.ToBase64String(data);
	}

	/// <summary> Pack a struct into a GZipped Base64 <see cref="string"/> </summary>
	/// <typeparam name="T"> Generic type of parameter </typeparam>
	/// <param name="value"> Parameter to pack </param>
	/// <returns> Base64 <see cref="string"/> from converting struct into <see cref="byte[]"/>, then gzipping, then encoding. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string GZipBase64<T>(T value) where T : struct {
		byte[] copy = Unsafe.ToBytes(value);
		return GZipBase64(copy);
	}

	/// <summary> Change <see cref="byte[]"/> into Base64 <see cref="string"/>, 
	/// first by running through <see cref="GZip.Compress(byte[])"/>, then by 
	/// encoding the results. </summary>
	/// <param name="data"> Data to pack as Gzipped Base64 <see cref="string"/> </param>
	/// <returns> Base64 <see cref="string"/> containing Gzipped data </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string GZipBase64(byte[] data) {
		return Base64(GZip.Compress(data));
	}
	
}

/// <summary> Class holding code for unpacking structs </summary>
public static class Unpack {

	/// <summary> Attempt to convert a Base64 encoded <see cref="string"/> into a struct by output parameter </summary>
	/// <typeparam name="T"> Generic type of parameter to unpack </typeparam>
	/// <param name="encoded"> Encoded Base64 <see cref="string"/> </param>
	/// <param name="ret"> Return location </param>
	/// <returns> True if successful, false if failure, 
	/// and sets <paramref name="ret"/> to the resulting unpacked data, or default, respectively.  </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool TryBase64<T>(string encoded, out T ret) where T : struct {
		try {
			byte[] bytes = RawBase64(encoded);
			Unsafe.FromBytes(bytes, out ret);
			return true;
		} catch (Exception) {
			ret = default(T);
			return false;
		}
	}

	/// <summary> Unpack encoded Base64 <see cref="string"/> into a struct </summary>
	/// <typeparam name="T"> Generic type to unpack </typeparam>
	/// <param name="encoded"> Encoded Base64 <see cref="string"/> </param>
	/// <returns> Unpacked data, or default value if anything failed (eg data size mismatch). </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T Base64<T>(string encoded) where T : struct {
		try {
			byte[] bytes = RawBase64(encoded);
			return Unsafe.FromBytes<T>(bytes);
		} catch (Exception) {
			return default(T);
		}
	}
	
	/// <summary> Attempt to convert a GZipped, Base64 <see cref="string"/> into a struct </summary>
	/// <typeparam name="T"> Generic type of parameter to unpack </typeparam>
	/// <param name="encoded"> Encoded GZipped, Base64 <see cref="string"/> </param>
	/// <param name="ret"> Return location </param>
	/// <returns> True if successful, false if failure, 
	/// and sets <paramref name="ret"/> to the resulting unpacked data, or default, respectively. </returns>
	public static bool TryGZipBase64<T>(string encoded, out T ret) where T : struct {
		try {
			byte[] bytes = GZipBase64(encoded);
			Unsafe.FromBytes(bytes, out ret);
			return true;
		} catch (Exception) {
			ret = default(T);
			return false;
		}
	}

	/// <summary> Unpack encoded Gzipped Base64 <see cref="string"/> into a struct </summary>
	/// <typeparam name="T"> Generic type to unpack </typeparam>
	/// <param name="encoded"> Encoded Base64 <see cref="string"/> </param>
	/// <returns> Unpacked data, or default value if anything failed (eg data size mismatch). </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T GZipBase64<T>(string encoded) where T : struct {
		try {
			byte[] bytes = GZipBase64(encoded);
			return Unsafe.FromBytes<T>(bytes);
		} catch (Exception) {
			return default(T);
		}
	}

	/// <summary> Unpack a Base64 <see cref="string"/> back into a <see cref="byte[]"/> </summary>
	/// <param name="encoded"> Encoded Base64 <see cref="string"/> </param>
	/// <returns> Unpacked data, or null if the encoded <see cref="string"/> is invalid </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static byte[] RawBase64(string encoded) {
		try {
			return Convert.FromBase64String(encoded);
		} catch (Exception) {
			return null;
		}
	}
	
	/// <summary> Unpack a Base64 <see cref="string"/> holding Gzipped data back into a <see cref="byte[]"/> </summary>
	/// <param name="encoded"> Encoded Base64 <see cref="string"/> </param>
	/// <returns> Unpacked data, or null if the encoded <see cref="string"/> is invalid </returns>
	public static byte[] GZipBase64(string encoded) {
		try {
			return GZip.Decompress(RawBase64(encoded));
		} catch (Exception) {
			return null;
		}
	}

}
	
