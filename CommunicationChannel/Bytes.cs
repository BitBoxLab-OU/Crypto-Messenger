using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 
/// </summary>
public static class Bytes
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="me"></param>
    /// <returns></returns>
    public static byte[] Reverse(this byte[] me)
    {
        Array.Reverse(me, 0, me.Length);
        return me;
    }

    private static byte[] Combine(this byte[] me, byte[] first, params byte[][] element)
    {
        foreach (var item in element)
            first = first.Combine(item);
        return me.Combine(first);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="me"></param>
    /// <param name="byteArray"></param>
    /// <returns></returns>
    public static byte[] Combine(this byte[] me, byte[] byteArray)
    {
        var combined = new byte[me.Length + byteArray.Length];
        Buffer.BlockCopy(me, 0, combined, 0, me.Length);
        Buffer.BlockCopy(byteArray, 0, combined, me.Length, byteArray.Length);
        return combined;
    }

    private static byte[] Combine(params byte[][] arrays)
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
    private static List<byte[]> Split(this byte[] data)
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
    private static byte[] Join(this byte[] data, params byte[][] values)
    {
        var list = new List<byte[]>(values);
        list.Insert(0, data);
        return Join(list.ToArray());
    }

    /// <summary>
    /// Join data packets
    /// </summary>
    /// <param name="values">packages to join</param>
    /// <returns>Byte array splittable</returns>
    private static byte[] Join(params byte[][] values)
    {
        var data = Array.Empty<byte>();
        foreach (var value in values)
        {
            data = data.Combine(((ushort)value.Length).GetBytes(), value);
        }
        return data;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="me"></param>
    /// <returns></returns>
    public static byte[] GetBytes(this string me) => me == null ? Array.Empty<byte>() : System.Text.Encoding.Unicode.GetBytes(me);
    /// <summary>
    /// 
    /// </summary>
    /// <param name="me"></param>
    /// <returns></returns>
    public static byte[] GetBytesFromASCII(this string me) => System.Text.Encoding.ASCII.GetBytes(me);
   /// <summary>
   /// 
   /// </summary>
   /// <param name="me"></param>
   /// <returns></returns>
    public static string ToUnicode(this byte[] me) => System.Text.Encoding.Unicode.GetString(me);
    /// <summary>
    /// 
    /// </summary>
    /// <param name="me"></param>
    /// <returns></returns>
    public static string ToASCII(this byte[] me) => System.Text.Encoding.ASCII.GetString(me);
    private static string ToBase64(this byte[] me) => Convert.ToBase64String(me);

    private static string ToHex(this byte[] bytes)
    {
        var hex = new System.Text.StringBuilder(bytes.Length * 2);
        foreach (byte b in bytes)
            hex.AppendFormat("{0:x2}", b);
        return hex.ToString();
    }

    private static byte[] Base64ToBytes(this string base64)
    {
        return Convert.FromBase64String(base64);
    }

    private static byte[] HexToBytes(this string hex)
    {
        int NumberChars = hex.Length;
        byte[] bytes = new byte[NumberChars / 2];
        for (int i = 0; i < NumberChars; i += 2)
            bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
        return bytes;
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="source"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static byte[] Take(this byte[] source, int length)
    {
        var result = new byte[length];
        Array.Copy(source, result, length);
        return result;
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="source"></param>
    /// <param name="offset"></param>
    /// <returns></returns>
    public static byte[] Skip(this byte[] source, int offset)
    {
        var result = new byte[source.Length - offset];
        Buffer.BlockCopy(source, offset, result, 0, result.Length);
        return result;
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="source"></param>
    /// <param name="compareTo"></param>
    /// <returns></returns>
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
    /// 
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static byte[] GetBytes(this int value) => CommunicationChannel.Converter.GetBytes(value);
    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static byte[] GetBytes(this uint value) => CommunicationChannel.Converter.GetBytes(value);

    private static byte[] GetBytes(this long value) => CommunicationChannel.Converter.GetBytes(value);

    private static byte[] GetBytes(this ulong value) => CommunicationChannel.Converter.GetBytes(value);

    private static byte[] GetBytes(this short value) => CommunicationChannel.Converter.GetBytes(value);

    private static byte[] GetBytes(this ushort value) => CommunicationChannel.Converter.GetBytes(value);
}
