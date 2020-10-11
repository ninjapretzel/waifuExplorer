#if UNITY_2017 || UNITY_2018 || UNITY_2019 || UNITY_2020
#define UNITY
using UnityEngine;
#else
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.IO;
#endif

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/// <summary> Static generic template-like class to cache information about structs </summary>
/// <typeparam name="T"> Struct type to cache information for </typeparam>
public static class StructInfo<T> where T : struct {
	/// <summary> Size of struct in bytes </summary>
	public static readonly int size = Unsafe.SizeOf<T>();
}
#region Util Structs

/// <summary> Interop struct for packing a float[] into a struct, to allow proper use of network arrays embedded in structs </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct InteropFloat64 {
	public const int MAX_LENGTH = 64;
	public fixed float fixedBuffer[MAX_LENGTH];
	public float this[int i] {
		get {
			if (i < 0 || i >= MAX_LENGTH) { return 0; }
			fixed (float* f = fixedBuffer) { return f[i]; }
		}
		set {
			if (i < 0 || i >= MAX_LENGTH) { return; }
			fixed (float* f = fixedBuffer) { f[i] = value; }
		}
	}

	public static implicit operator float[] (InteropFloat64 f) {
		float[] floats = new float[MAX_LENGTH];
		for (int i = 0; i < MAX_LENGTH; i++) { floats[i] = f[i]; }
		return floats;
	}
	public static implicit operator InteropFloat64(float[] floats) {
		InteropFloat64 f;
		for (int i = 0; i < MAX_LENGTH && i < floats.Length; i++) { f[i] = floats[i]; }
		return f;
	}
}


/// <summary> Interop struct for packing a float[] into a struct, to allow proper use of network arrays embedded in structs </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct InteropFloat32 {
	public const int MAX_LENGTH = 32;
	public fixed float fixedBuffer[MAX_LENGTH];
	public float this[int i] {
		get {
			if (i < 0 || i >= MAX_LENGTH) { return 0; }
			fixed (float* f = fixedBuffer) { return f[i]; }
		}
		set {
			if (i < 0 || i >= MAX_LENGTH) { return; }
			fixed (float* f = fixedBuffer) { f[i] = value; }
		}
	}

	public static implicit operator float[] (InteropFloat32 f) {
		float[] floats = new float[MAX_LENGTH];
		for (int i = 0; i < MAX_LENGTH; i++) { floats[i] = f[i]; }
		return floats;
	}
	public static implicit operator InteropFloat32(float[] floats) {
		InteropFloat32 f;
		for (int i = 0; i < MAX_LENGTH && i < floats.Length; i++) { f[i] = floats[i]; }
		return f;
	}
}


/// <summary> Interop struct for packing a string into a struct, to allow proper use of network strings embedded in structs </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct InteropString32 {
	public const int MAX_LENGTH = 32;
	/// <summary> Embedded char array </summary>
	public fixed char fixedBuffer[MAX_LENGTH];

	/// <summary> Get or set the string value of this struct </summary>
	public string value {
		get {
			fixed (char* c = fixedBuffer) {
				return new string(c);
			}

		}
		set {
			fixed (char* c = fixedBuffer) {
				int len = value.Length;
				for (int i = 0; i < MAX_LENGTH - 1; i++) {
					if (i < len) {
						c[i] = value[i];
					} else {
						c[i] = '\0';
					}
				}
				// Ensure final char in buffer is always null.
				c[MAX_LENGTH - 1] = '\0';
			}
		}
	}

	public override string ToString() { return value; }
	public override int GetHashCode() { return value.GetHashCode(); }
	public override bool Equals(object obj) {
		if (obj is InteropString32) { return value.Equals(((InteropString32)obj).value); }
		// may be bad...
		if (obj is string) { return ToString().Equals(obj.ToString()); }
		return false;
	}

	public static implicit operator string(InteropString32 s) { return s.value; }
	public static implicit operator InteropString32(string str) { InteropString32 s; s.value = str; return s; }
}
/// <summary> Interop struct for packing a string into a struct, to allow proper use of network strings embedded in structs </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct InteropString256 {
	public const int MAX_LENGTH = 256;
	/// <summary> Embedded char array </summary>
	public fixed char fixedBuffer[MAX_LENGTH];

	/// <summary> Get or set the string value of this struct </summary>
	public string value {
		get {
			fixed (char* c = fixedBuffer) {
				return new string(c);
			}
		}
		set {
			fixed (char* c = fixedBuffer) {
				int len = value.Length;
				for (int i = 0; i < MAX_LENGTH - 1; i++) {
					if (i < len) {
						c[i] = value[i];
					} else {
						c[i] = '\0';
					}
				}
				// Ensure final char in buffer is always null.
				c[MAX_LENGTH - 1] = '\0';
			}
		}
	}

	public override string ToString() { return value; }
	public override int GetHashCode() { return value.GetHashCode(); }
	public override bool Equals(object obj) {
		if (obj is InteropString256) { return value.Equals(((InteropString256)obj).value); }
		// may be bad...
		if (obj is string) { return ToString().Equals(obj.ToString()); }
		return false;
	}

	public static implicit operator string(InteropString256 s) { return s.value; }
	public static implicit operator InteropString256(string str) { InteropString256 s; s.value = str; return s; }
}

#endregion

/// <summary> 
/// Not your safe-space. 
/// Primary place for putting methods that need to make use of unsafe blocks of code.
/// Modified code from http://benbowen.blog/post/fun_with_makeref/
/// </summary>
public static class Unsafe {
	/// <summary> Are we running on the Mono Runtime? </summary>
	/// @TODO: Eventually check to see the version.
	/// We may need to further branch if mono changes the TypedReference struct in a later version.
	public static readonly bool MonoRuntime = Type.GetType("Mono.Runtime") != null;

	/// <summary>Extracts the bytes from a generic value type.</summary>
	/// <typeparam name="T">Generic type. </typeparam>
	/// <param name="obj">Instance of generic type <paramref name="T"/> to convert</param>
	/// <returns>Raw byte array of the given object</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static unsafe byte[] ToBytes<T>(T value) where T : struct {
		byte[] bytes = new byte[StructInfo<T>.size];
		TypedReference valueRef = __makeref(value);
		// Debug.Log($"Memory around ref of {typeof(T)}:\n{InspectMemory(&valueRef)}");

		// Unsafe Abuse
		// First of all we're getting a pointer to valueref (so that's a reference to our reference), 
		// and treating it as a pointer to an IntPtr instead of a pointer to a TypedReference. 
		// This works because the first 4/8 bytes in the TypedReference struct are an IntPtr 
		// specifically the pointer to value. Then we dereference that IntPtr pointer to a regular old IntPtr, 
		// and finally cast that IntPtr to a byte* so we can use it in the copy code below.

		// @oddity @hack
		// Mono's implementation of the TypedReference struct has the type first and the reference second
		// So we have to dereference the second segment to get the actual reference.
		byte* valuePtr = MonoRuntime ? ((byte*)*(((IntPtr*)&valueRef) + 1)) : ((byte*)*((IntPtr*)&valueRef));

		for (int i = 0; i < bytes.Length; ++i) {
			bytes[i] = valuePtr[i];
		}
		return bytes;
	}

	/// <summary> Extracts bytes from a struct value into an existing byte[] array, starting at a position </summary>
	/// <typeparam name="T"> Generic type of value parameter </typeparam>
	/// <param name="value"> Value to extract data from </param>
	/// <param name="bytes"> byte[] to place data into </param>
	/// <param name="start"> starting index </param>
	/// <returns> Modified byte[] </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static unsafe byte[] ToBytes<T>(T value, byte[] bytes, int start) where T : struct {
		TypedReference valueRef = __makeref(value);

		// @oddity @hack
		// Mono's implementation of the TypedReference struct has the type first and the reference second
		// So we have to dereference the second segment to get the actual reference.
		byte* valuePtr = MonoRuntime ? ((byte*)*(((IntPtr*)&valueRef) + 1)) : ((byte*)*((IntPtr*)&valueRef));

		for (int i = 0; i + start < bytes.Length; i++) {
			bytes[i + start] = valuePtr[i];
		}

		return bytes;
	}
	/// <summary> Extracts an arbitrary struct from a byte array, at a given position </summary>
	/// <typeparam name="T"> Generic type of struct to extract </typeparam>
	/// <param name="source"> Source byte[] </param>
	/// <param name="start"> Index struct exists at </param>
	/// <returns> Struct built from byte array, starting at index </returns>
	public static unsafe T FromBytes<T>(byte[] source, int start) where T : struct {
		int sizeOfT = StructInfo<T>.size;
		if (start < 0) {
			throw new Exception($"Unsafe.FromBytes<{typeof(T)}>(): start index must be 0 or greater, was {start}");
		}
		if (sizeOfT + start > source.Length) {
			throw new Exception($"Unsafe.FromBytes<{typeof(T)}>(): Source is {source.Length} bytes, start at {start}, and target is {sizeOfT} bytes in size, out of range.");
		}

		// has exactly the same idea behind it as the similar line in the ToBytes method- 
		// we're getting the pointer to result.
		T result = default(T);
		TypedReference resultRef = __makeref(result);
		// @oddity @hack
		// Mono's implementation of the TypedReference struct has the type first and the reference second
		// So we have to dereference the second segment to get the actual reference.
		byte* resultPtr = MonoRuntime ? ((byte*)*(((IntPtr*)&resultRef) + 1)) : ((byte*)*((IntPtr*)&resultRef));

		for (int i = 0; i < sizeOfT; ++i) {
			resultPtr[i] = source[start + i];
		}

		return result;
	}

	/// <summary>Converts a byte[] back into a struct.</summary>
	/// <typeparam name="T">Generic type</typeparam>
	/// <param name="source">Data source</param>
	/// <returns>Object of type T assembled from bytes in source</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static unsafe T FromBytes<T>(byte[] source) where T : struct {
		int sizeOfT = StructInfo<T>.size;
		if (sizeOfT != source.Length) {
			throw new Exception($"Unsafe.FromBytes<{typeof(T)}>(): Source is {source.Length} bytes, but expected type is {sizeOfT} bytes in size.");
		}

		// has exactly the same idea behind it as the similar line in the ToBytes method- 
		// we're getting the pointer to result.
		T result = default(T);
		TypedReference resultRef = __makeref(result);
		byte* resultPtr = MonoRuntime ? ((byte*)*(((IntPtr*)&resultRef) + 1)) : ((byte*)*((IntPtr*)&resultRef));

		for (int i = 0; i < sizeOfT; ++i) {
			resultPtr[i] = source[i];
		}

		return result;
	}

	/// <summary>Converts a byte[] back into a struct.</summary>
	/// <typeparam name="T">Generic type</typeparam>
	/// <param name="source">Data source</param>
	/// <returns>Object of type T assembled from bytes in source</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static unsafe void FromBytes<T>(byte[] source, out T ret) where T : struct {
		int sizeOfT = StructInfo<T>.size;
		if (sizeOfT != source.Length) {
			throw new Exception($"Unsafe.FromBytes<{typeof(T)}>(): Source is {source.Length} bytes, but expected type is {sizeOfT} bytes in size.");
		}

		// has exactly the same idea behind it as the similar line in the ToBytes method- 
		// we're getting the pointer to result.
		T result = default(T);
		TypedReference resultRef = __makeref(result);
		// @oddity @hack
		// Mono's implementation of the TypedReference struct has the type first and the reference second
		// So we have to dereference the second segment to get the actual reference.
		byte* resultPtr = MonoRuntime ? ((byte*)*(((IntPtr*)&resultRef) + 1)) : ((byte*)*((IntPtr*)&resultRef));

		for (int i = 0; i < sizeOfT; ++i) {
			resultPtr[i] = source[i];
		}
		ret = result;
	}

	/// <summary> Helper class for generic SizeOf&lt;T&gt; method</summary>
	/// <typeparam name="T">Struct type to hold two of </typeparam>
	private static class ArrayOfTwoElements<T> where T : struct { public static readonly T[] Value = new T[2]; }
	/// <summary> Helper class for generic SizeOf&lt;T&gt; method</summary>
	/// <typeparam name="T"> Struct type to whole two of </typeparam>
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	private struct Two<T> where T : struct { public T first, second; public static readonly Two<T> instance = default(Two<T>); }

	/// <summary> Generic, runtime sizeof() for value types. </summary>
	/// <typeparam name="T">Type to check size of </typeparam>
	/// <returns>Size of the type passed, in bytes. Returns the pointer size for </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static int SizeOf<T>() where T : struct {
		Type type = typeof(T);

		TypeCode typeCode = Type.GetTypeCode(type);
		switch (typeCode) {
			case TypeCode.Boolean:
				return sizeof(bool);
			case TypeCode.Char:
				return sizeof(char);
			case TypeCode.SByte:
				return sizeof(sbyte);
			case TypeCode.Byte:
				return sizeof(byte);
			case TypeCode.Int16:
				return sizeof(short);
			case TypeCode.UInt16:
				return sizeof(ushort);
			case TypeCode.Int32:
				return sizeof(int);
			case TypeCode.UInt32:
				return sizeof(uint);
			case TypeCode.Int64:
				return sizeof(long);
			case TypeCode.UInt64:
				return sizeof(ulong);
			case TypeCode.Single:
				return sizeof(float);
			case TypeCode.Double:
				return sizeof(double);
			case TypeCode.Decimal:
				return sizeof(decimal);
			default:
				unsafe {
#if USE_ARRAY
				T[] array = ArrayOfTwoElements<T>.Value;
				GCHandle pin = GCHandle.Alloc(array, GCHandleType.Pinned);
				try {
					var ref0 = __makeref(array[0]);
					var ref1 = __makeref(array[1]);
					// @oddity @hack
					// Mono's implementation of the TypedReference struct has the type first and the reference second
					// So we have to dereference the second segment to get the actual reference.
					IntPtr p0 = MonoRuntime ? (*( ((IntPtr*)&ref0) + 1)) : (*((IntPtr*)&ref0));
					IntPtr p1 = MonoRuntime ? (*( ((IntPtr*)&ref1) + 1)) : (*((IntPtr*)&ref1));
						
					return (int)(((byte*)p1) - ((byte*)p0));
				} finally { pin.Free(); }
#else
					Two<T> two = Two<T>.instance;
					TypedReference ref0 = __makeref(two.first);
					TypedReference ref1 = __makeref(two.second);
					// @oddity @hack
					// Mono's implementation of the TypedReference struct has the type first and the reference second
					// So we have to dereference the second segment to get the actual reference.
					IntPtr p0 = MonoRuntime ? (*(((IntPtr*)&ref0) + 1)) : (*((IntPtr*)&ref0));
					IntPtr p1 = MonoRuntime ? (*(((IntPtr*)&ref1) + 1)) : (*((IntPtr*)&ref1));
#endif

					return (int)(((byte*)p1) - ((byte*)p0));

				}
		}
	}

	/// <summary> Inspect the raw memory around a pointer </summary>
	/// <param name="p"> Pointer to inspect </param>
	/// <param name="length"> Total number of bytes to inspect </param>
	/// <param name="stride"> Number of bytes to put on a single line </param>
	/// <returns> String holding hexdump of the memory at the given location </returns>
	public static unsafe string InspectMemory(IntPtr p, int length = 16, int stride = 8) {
		return InspectMemory((void*)p, length, stride);
	}

	/// <summary> Inspect the raw memory around a pointer </summary>
	/// <param name="p"> Pointer to inspect </param>
	/// <param name="length"> Total number of bytes to inspect </param>
	/// <param name="stride"> Number of bytes to put on a single line </param>
	/// <returns> String holding hexdump of the memory at the given location </returns>
	public static unsafe string InspectMemory(void* p, int length = 16, int stride = 8) {
		System.Text.StringBuilder str = new System.Text.StringBuilder();
		byte* bp = (byte*)p;
		for (int i = 0; i < length; i++) {
			if (i % stride == 0) {
				str.Append(i == 0 ? "0x" : "\n0x");
			}
			str.Append(String.Format("{0:X2}", bp[i]));
		}
		return str.ToString();
	}


	/// <summary> Reinterprets an object's data from one type to another.</summary>
	/// <typeparam name="TIn">Input struct type</typeparam>
	/// <typeparam name="TOut">Output struct type</typeparam>
	/// <param name="val">Value to convert</param>
	/// <returns><paramref name="val"/>'s bytes converted into a <paramref name="TOut"/></returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static unsafe TOut Reinterpret<TIn, TOut>(TIn val)
		where TIn : struct
		where TOut : struct {

		TOut result = default(TOut);
		int sizeBytes = StructInfo<TIn>.size;
		if (sizeBytes != StructInfo<TOut>.size) { return result; }

		TypedReference resultRef = __makeref(result);
		TypedReference valRef = __makeref(val);
		// @oddity @hack
		// Mono's implementation of the TypedReference struct has the type first and the reference second
		// So we have to dereference the second segment to get the actual reference.
		byte* resultPtr = MonoRuntime ? ((byte*)*(((IntPtr*)&resultRef + 1))) : ((byte*)*(((IntPtr*)&resultRef)));
		byte* valPtr = MonoRuntime ? ((byte*)*(((IntPtr*)&valRef + 1))) : ((byte*)*(((IntPtr*)&valRef)));

		for (int i = 0; i < sizeBytes; ++i) {
			resultPtr[i] = valPtr[i];
		}

		return result;
	}

}
