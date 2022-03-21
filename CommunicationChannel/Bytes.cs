using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// This class contains the operations, which are helpful in processing bytes array.
/// </summary>
public static class Bytes
{
    /// <summary>
    /// Reverse the recieve byte.
    /// </summary>
    /// <param name="me">extend the method of byte array.</param>
    /// <returns>reverserd byte</returns>
    public static byte[] Reverse(this byte[] me)
    {
        Array.Reverse(me, 0, me.Length);
        return me;
    }

    /// <summary>
    /// Combine the data packets into single byte array.
    /// </summary>
    /// <param name="me"> extend the method of byte array. </param>
    /// <param name="first">byte array</param>
    /// <param name="element">byte array list</param>
    /// <returns>combined byte array</returns>
    public static byte[] Combine(this byte[] me, byte[] first, params byte[][] element)
    {
        foreach (var item in element)
            first = first.Combine(item);
        return me.Combine(first);
    }

    ///<inheritdoc cref="Combine(byte[], byte[], byte[][])"/>
    public static byte[] Combine(this byte[] me, byte[] byteArray)
    {
        var combined = new byte[me.Length + byteArray.Length];
        Buffer.BlockCopy(me, 0, combined, 0, me.Length);
        Buffer.BlockCopy(byteArray, 0, combined, me.Length, byteArray.Length);
        return combined;
    }
    ///<inheritdoc cref="Combine(byte[], byte[], byte[][])"/>
    public static byte[] Combine(params byte[][] arrays)
    {
        byte[] rv = new byte[arrays.Sum(a => a.Length)];
        int offset = 0;
        foreach (byte[] array in arrays)
        {
            Buffer.BlockCopy(array, 0, rv, offset, array.Length);
            offset += array.Length;
        }
        return rv;
    }

    /// <summary>
    /// Divide merged data packets with join function
    /// </summary>
    /// <param name="data">Combined packages</param>
    /// <returns>Split data List</returns>
    public static List<byte[]> Split(this byte[] data)
    {
        int offset = 0;
        var datas = new List<byte[]>();
        while (offset < data.Length)
        {
            ushort len = BitConverter.ToUInt16(data, offset);
            offset += 2;
            var part = new byte[len];
            Buffer.BlockCopy(data, offset, part, 0, len);
            datas.Add(part);
            offset += len;
        }
        return datas;
    }

    /// <summary>
    /// Join data packets
    /// </summary>
    /// <param name="data"> packages to join<</param>
    /// <param name="values"></param>
    /// <returns>Byte array splittable</returns>
    public static byte[] Join(this byte[] data, params byte[][] values)
    {
        var list = new List<byte[]>(values);
        list.Insert(0, data);
        return Join(list.ToArray());
    }

    ///<inheritdoc cref="Join(byte[], byte[][])"/>
    public static byte[] Join(params byte[][] values)
    {
        var data = Array.Empty<byte>();
        foreach (var value in values)
        {
            data = data.Combine(((ushort)value.Length).GetBytes(), value);
        }
        return data;
    }

    /// <summary>
    /// Get bytes from ASCII.
    /// </summary>
    /// <param name="me"> string to get byte from.</param>
    /// <returns>byte array</returns>
    public static byte[] GetBytesFromASCII(this string me) => System.Text.Encoding.ASCII.GetBytes(me);
   /// <summary>
   /// Get unicode from byte array.
   /// </summary>
   /// <param name="me">byte array</param>
   /// <returns> unicode string </returns>
    public static string ToUnicode(this byte[] me) => System.Text.Encoding.Unicode.GetString(me);
    /// <summary>
    /// Get ASCII from byte array.
    /// </summary>
    /// <param name="me">byte array</param>
    /// <returns> ASCII</returns>
    public static string ToASCII(this byte[] me) => System.Text.Encoding.ASCII.GetString(me);

    /// <summary>
    /// Get Base64 from byte array.
    /// </summary>
    /// <param name="me">byte array</param>
    /// <returns> Base64 string</returns>
    public static string ToBase64(this byte[] me) => Convert.ToBase64String(me);

    /// <summary>
    /// Get hex from byte array.
    /// </summary>
    /// <param name="bytes">byte array</param>
    /// <returns> hex string</returns>
    public static string ToHex(this byte[] bytes)
    {
        var hex = new System.Text.StringBuilder(bytes.Length * 2);
        foreach (byte b in bytes)
            hex.AppendFormat("{0:x2}", b);
        return hex.ToString();
    }

    /// <summary>
    /// Get byte array from Base64 string.
    /// </summary>
    /// <param name="base64">Base64 string</param>
    /// <returns> byte array</returns>
    public static byte[] Base64ToBytes(this string base64)
    {
        return Convert.FromBase64String(base64);
    }

    /// <summary>
    /// Get byte array from hex.
    /// </summary>
    /// <param name="hex">hex string</param>
    /// <returns>byte array</returns>
    public static byte[] HexToBytes(this string hex)
    {
        int NumberChars = hex.Length;
        byte[] bytes = new byte[NumberChars / 2];
        for (int i = 0; i < NumberChars; i += 2)
            bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
        return bytes;
    }
    /// <summary>
    /// Take the define number of elements from the byte array.
    /// </summary>
    /// <param name="source">byte array</param>
    /// <param name="length"> number of element to take</param>
    /// <returns> byte array</returns>
    public static byte[] Take(this byte[] source, int length)
    {
        var result = new byte[length];
        Array.Copy(source, result, length);
        return result;
    }
    /// <summary>
    /// Skip the byte from the byte array
    /// </summary>
    /// <param name="source"> byte array</param>
    /// <param name="offset"> number of the byte to skip</param>
    /// <returns> byte array </returns>
    public static byte[] Skip(this byte[] source, int offset)
    {
        var result = new byte[source.Length - offset];
        Buffer.BlockCopy(source, offset, result, 0, result.Length);
        return result;
    }
    /// <summary>
    /// Compare the sequence of the two byte array.
    /// </summary>
    /// <param name="source"> source byte array</param>
    /// <param name="compareTo"> byte array to be compared with</param>
    /// <returns> True or False </returns>
    public static bool SequenceEqual(this byte[] source, byte[] compareTo)
    {
        if (compareTo.Length != source.Length)
            return false;
        for (var i = 0; i < source.Length; i++)
            if (source[i] != compareTo[i])
                return false;
        return true;
    }
    /// <summary>
    /// Transform a primordial value into the byte array representing it.
    /// </summary>
    /// <param name="value">Value to convert</param>
    /// <returns>Bytes array to represent the value</returns>
    public static byte[] GetBytes(this int value) => CommunicationChannel.Converter.GetBytes(value);
    ///<inheritdoc cref="GetBytes(int)"/>
    public static byte[] GetBytes(this uint value) => CommunicationChannel.Converter.GetBytes(value);
    ///<inheritdoc cref="GetBytes(int)"/>
    public static byte[] GetBytes(this long value) => CommunicationChannel.Converter.GetBytes(value);
    ///<inheritdoc cref="GetBytes(int)"/>
    public static byte[] GetBytes(this ulong value) => CommunicationChannel.Converter.GetBytes(value);
    ///<inheritdoc cref="GetBytes(int)"/>
    public static byte[] GetBytes(this short value) => CommunicationChannel.Converter.GetBytes(value);
    ///<inheritdoc cref="GetBytes(int)"/>
    public static byte[] GetBytes(this ushort value) => CommunicationChannel.Converter.GetBytes(value);
    ///<inheritdoc cref="GetBytes(int)"/>
    public static byte[] GetBytes(this string me) => me == null ? Array.Empty<byte>() : System.Text.Encoding.Unicode.GetBytes(me);
}
