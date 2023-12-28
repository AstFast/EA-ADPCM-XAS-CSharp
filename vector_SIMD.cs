using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace EA_ADPCM_XAS_CSharp
{

	public struct Vec128
	{
		public Vector128<int> I128;
		public static explicit operator Vec128(Int32x4 distance)
		{
			return new Vec128 {I128 = distance.I128 };
		}
		public static Vec128 operator &(Vec128 a, Int32x4 b)
		{
			return new Vec128(Sse2.And(a.I128, b.I128));
		}
		public static Vec128 operator &(Vec128 a, Vec128 b)
		{
			return new Vec128 { I128 = Sse2.And(a.I128, b.I128) };
		}

		public static Vec128 operator |(Vec128 a, Vec128 b)
		{
			return new Vec128 { I128 = Sse2.Or(a.I128, b.I128) };
		}

		public static Vec128 operator ^(Vec128 a, Vec128 b)
		{
			return new Vec128 { I128 = Sse2.Xor(a.I128, b.I128) };
		}
		public Vec128(Vector128<int> value)
		{
			I128 = value;
		}
		public static implicit operator Vector128<int>(Vec128 distance)
		{
			return distance.I128;
		}
		public static implicit operator Vec128(Vector128<int> distance)
		{
			return new Vec128 {I128 = distance };
		}

		public unsafe T SIMD_reinterpret_cast<T>() where T : struct
		{
			fixed (Vector128<int>* ptr = &I128)
			{
				return *(T*)ptr;
			}
		}
		public Vec128 OnesComplement()
		{
			return new Vec128 { I128 = Sse2.Xor(I128, Sse2.CompareEqual(I128, I128)) };
		}
	}

	public unsafe static class Vec128Extensions
	{
		public static Vec128 LoadUnaligned(int* ptr)
		{
			return new Vec128 { I128 = Sse2.LoadVector128(ptr) };
		}

		public static Vec128 GetOnes128()
		{
			return new Vec128 { I128 = Vector128.Create(-1) };
		}
		public static Vec128 GetZeros128()
		{
			return new Vec128 { I128 = Vector128<int>.Zero };
		}
	}

	public struct Int32x4
	{
		public Vector128<int> I128;

		public Int32x4(Vector128<int> vector128)
		{
			I128 = vector128;
		}
		public static Int32x4 operator +(Int32x4 a, Int32x4 b)
		{
			Vector128<int> result = Sse2.Add(a.I128, b.I128);
			return new Int32x4 { I128 = result };
		}
		public static Int32x4 operator >>(Int32x4 a, int shift_imm8)
		{
			Vector128<int> result = Sse2.ShiftRightArithmetic(a.I128, (byte)shift_imm8);
			return new Int32x4 { I128 = result };
		}
		public unsafe static Int32x4 operator >>(Int32x4 a, Int32x4 shift)
		{
			int* valPtr = stackalloc int[4];
			int* shPtr = stackalloc int[4];
			Sse2.Store(valPtr, a.I128);
			Sse2.Store(shPtr, shift.I128);
			for (int i = 0; i < 4; i++)
			{
				valPtr[i] >>= shPtr[i];
			}
			Vector128<int> result = Sse2.LoadVector128(valPtr);
			return new Int32x4 { I128 = result };
		}

		public static unsafe implicit operator Int32x4(UInt16x8 value)
		{
			Vector128<ushort> ushortVector = value.I128.AsUInt16();
			int* ushortPtr = stackalloc int[8];
			Sse2.Store((ushort*)(ushortPtr + 0), ushortVector);

			Vector128<int> resultVector = Vector128.Create(
				ushortPtr[0], ushortPtr[1], ushortPtr[2], ushortPtr[3]
			);

			return new Int32x4 { I128 = resultVector };
		}
		public static Int32x4 operator <<(Int32x4 a, int shift_imm8)
		{
			return new Int32x4 { I128 = Sse2.ShiftLeftLogical(a.I128, (byte)shift_imm8) };
		}
		public unsafe T SIMD_reinterpret_cast<T>() where T : struct
		{
			fixed (Vector128<int>* ptr = &I128)
			{
				return *(T*)ptr;
			}
		}
		public static Int32x4 operator &(Int32x4 a, Int32x4 b)
		{
			return new Int32x4 { I128 = Sse2.Add(a.I128, b.I128) };
		}
		public static Int32x4 operator |(Int32x4 a, Int32x4 b)
		{
			return new Int32x4 { I128 = Sse2.Or(a.I128, b.I128) };
		}
		public static implicit operator Vector128<int>(Int32x4 distance)
		{
			return distance.I128;
		}
		public static explicit operator Int32x4(Vector128<int> distance)
		{
			var i = new Int32x4();
			i.I128 = distance;
			return i;
		}
		
		public static implicit operator Int32x4(Vec128 v)
		{
			var i = new Int32x4();
			i.I128 = (Int32x4)v.I128;
			return i;
		}
		public int GetElement(int index)
		{
			return I128.GetElement(index);
		}
	}

	public struct UInt32x4
	{
		public Int32x4 I128;
		public static Vector128<int> operator &(Vector128<int> a, UInt32x4 b)
		{
			return Sse2.And(a, b.I128);
		}

		public static UInt32x4 operator &(UInt32x4 a, Vector128<int> b)
		{
			return new UInt32x4 { I128 = (Int32x4)Sse2.And(a.I128, b) };
		}
		public static implicit operator Int32x4(UInt32x4 distance)
		{
			return distance.I128;
		}
		public static implicit operator Vec128(UInt32x4 distance)
		{
			return distance.I128.I128;
		}
		//Vector128->UInt32x4
		public static explicit operator UInt32x4(Vector128<int> distance)
		{
			var i = new UInt32x4();
			i.I128 = (Int32x4)distance;
			return i;
		}
		//Vec128->UInt32x4
		public static explicit operator UInt32x4(Vec128 v)
		{
			var i = new UInt32x4();
			i.I128 = (Int32x4)v.I128;
			return i;
		}
		public static UInt32x4 operator >>(UInt32x4 a, int shift_imm8)
		{
			return new UInt32x4 { I128 = (Int32x4)Sse2.ShiftRightLogical(a.I128, (byte)shift_imm8) };
		}

	}

	public struct Int16x8
	{
		public Vector128<short> I128;
		public static Int16x8 operator >>(Int16x8 a, int shift_imm8)
		{
			return new Int16x8 { I128 = Sse2.ShiftRightLogical(a.I128, (byte)shift_imm8) };
		}
		public static Int16x8 operator <<(Int16x8 a, int shift_imm8)
		{
			return new Int16x8 { I128 = Sse2.ShiftLeftLogical(a.I128, (byte)shift_imm8) };
		}
		public short GetElement(int index)
		{
			return I128.GetElement(index);
		}
		/*
public static explicit operator Int16x8(Int32x4 v)
{
	var i = new Int16x8();
	i.I128 = v.I128;
	return i;
}
*/
		public unsafe T SIMD_reinterpret_cast<T>() where T : struct
		{
			fixed (Vector128<short>* ptr = &I128)
			{
				return *(T*)ptr;
			}
		}
		public Int16x8(Vector128<short> value)
		{
			I128 = value;
		}
	}
	public struct UInt16x8
	{
		public Vector128<ushort> I128;
		/*
		public static implicit operator Int32x4(UInt16x8 value)
		{
			return new Int32x4(Sse2.ConvertToVector128Int32(value.I128));
		}
		*/
	}


	/*
	public struct UInt16x8
	{
		public Vec128 I128;

		public static implicit operator Int32x4(UInt16x8 value)
		{
			return new Int32x4 { I128 = Sse2.ConvertToVector128Int32(value.I128.I128) };
		}
	}

	public struct uint8x16
	{
		public Vec128 I128;

		public static implicit operator Int32x4(uint8x16 value)
		{
			return new Int32x4 { I128 = Sse2.ConvertToVector128Int32(value.I128.I128) };
		}
	}
	*/


	public static class Intrinsics
	{

		public static Int16x8 ShiftLeft(Int16x8 a,int shiftImm8)
		{
			return new Int16x8 { I128 = Sse2.ShiftLeftLogical(a.I128, (byte)shiftImm8) };
		}

		public static Int32x4 ShiftLeft(Int32x4 a, int shiftImm8)
		{
			return new Int32x4 { I128 = Sse2.ShiftLeftLogical(a.I128, (byte)shiftImm8) };
		}

		public static Int16x8 ShiftRight(Int16x8 a, int shiftImm8)
		{
			return new Int16x8 { I128 = Sse2.ShiftRightLogical(a.I128, (byte)shiftImm8) };
		}

		public static Int32x4 ShiftRight(Int32x4 a, int shiftImm8)
		{
			return new Int32x4 { I128 = Sse2.ShiftRightLogical(a.I128, (byte)shiftImm8) };
		}
		/*
		public static UInt32x4 ShiftRight(UInt32x4 a, int shiftImm8)
		{
			return new UInt32x4 { I128 = Sse2.ShiftRightLogical(a.I128.I128, (byte)shiftImm8) };
		}

		public static Int32x4 ShiftRight(Int32x4 a, Int32x4 shift)
		{
			return new Int32x4 { I128 = Sse2.ArithmeticShiftRight(a.I128, shift.I128) };
		}
		*/
		public static Int32x4 Add(Int32x4 a, Int32x4 b)
		{
			return new Int32x4 { I128 = Sse2.Add(a.I128, b.I128) };
		}

		public static Int16x8 Add(Int16x8 a, Int16x8 b)
		{
			return new Int16x8 { I128 = Sse2.Add(a.I128, b.I128) };
		}

		public static Int32x4 Subtract(Int32x4 a, Int32x4 b)
		{
			return new Int32x4 { I128 = Sse2.Subtract(a.I128, b.I128) };
		}

		public static Int16x8 Subtract(Int16x8 a, Int16x8 b)
		{
			return new Int16x8 { I128 = Sse2.Subtract(a.I128, b.I128) };
		}
		/*
		public static Int32x4 MultiplyAddAdjacent(Int32x4 a, Int32x4 b)
		{
			return new Int32x4 { I128 = Sse2.MultiplyAddAdjacent(a.I128, b.I128) };
		}
		*/
		public static Vec128 And(Vec128 a, Vec128 b)
		{
			return new Vec128 { I128 = Sse2.And(a.I128, b.I128) };
		}

		public static Vec128 Or(Vec128 a, Vec128 b)
		{
			return new Vec128 { I128 = Sse2.Or(a.I128, b.I128) };
		}

		public static Vec128 Xor(Vec128 a, Vec128 b)
		{
			return new Vec128 { I128 = Sse2.Xor(a.I128, b.I128) };
		}

		public static Vec128 OnesComplement(Vec128 a)
		{
			return new Vec128 { I128 = Sse2.Xor(a.I128, Sse2.CompareEqual(a.I128, a.I128)) };
		}
	}
}
