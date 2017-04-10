﻿using System;

using Org.BouncyCastle.Crypto.Parameters;
using System.Collections.Generic;
using Org.BouncyCastle.Utilities;

namespace Org.BouncyCastle.Crypto.Engines
{
     public class Dstu7624WrapEngine : IWrapper 
     {
          private const int BYTES_IN_INTEGER = 4;

          private KeyParameter param;
          private Dstu7624Engine engine;
          private bool forWrapping;
          private int blockSize;

          private byte[] buffer, B;
          private byte[] intArray;

          private readonly byte[] checkSumArray, zeroArray;

          List<byte[]> bTemp;


          public Dstu7624WrapEngine(int blockSizeBits, int keySizeBits)
          {
               engine = new Dstu7624Engine(blockSizeBits, keySizeBits);
               param = null;
           
               blockSize = blockSizeBits / 8;
               buffer = new byte[blockSize];
               B = new byte[blockSize / 2];
               bTemp = new List<byte[]>();
               
               intArray = new byte[BYTES_IN_INTEGER];

               checkSumArray = new byte[blockSize];
               zeroArray = new byte[blockSize];
          }
          

          public string AlgorithmName
          {
               get { return "Dstu7624WrapEngine"; }
          }

          public void Init(bool forWrapping, ICipherParameters parameters)
          {
               this.forWrapping = forWrapping;
                            
               if (parameters is KeyParameter)
               {
                    this.param = (KeyParameter)parameters;

                    engine.Init(forWrapping, param);
               }
               else if (parameters != null)
               {
                    throw new ArgumentException("invalid parameter passed to Dstu7624WrapEngine init - "
                    + Platform.GetTypeName(parameters));
               }
          }

          public byte[] Wrap(byte[] input, int inOff, int length)
          {
               if (!forWrapping)
               {
                    throw new InvalidOperationException("Not set for wrapping");
               }

               if ((input.Length - inOff) % blockSize != 0)
               {
                    throw new ArgumentException("Padding not supported");
               }

               Check.DataLength(input, inOff, length, "input buffer too short");

               int n = 2 * (1 + length / blockSize);
               
               int V = (n - 1) * 6;

               buffer = new byte[input.Length - inOff + blockSize];
               Array.Copy(input, inOff, buffer, 0, input.Length - inOff);
                             
               Array.Copy(buffer, 0, B, 0, blockSize / 2);

               bTemp.Clear();
              
               int bHalfBlocksLen = buffer.Length - blockSize  / 2;
               int bufOff = blockSize / 2;
               while (bHalfBlocksLen != 0)
               {                    
                    byte[] temp = new byte[blockSize / 2];
                    Array.Copy(buffer, bufOff, temp, 0, blockSize / 2);
                   
                    bTemp.Add(temp);

                    bHalfBlocksLen -= blockSize / 2;
                    bufOff += blockSize / 2;
               }

               for (int j = 0; j < V; j++)
               {
                    Array.Copy(B, 0, buffer, 0, blockSize / 2);
                    Array.Copy(bTemp[0], 0, buffer, blockSize / 2, blockSize / 2);

                    engine.ProcessBlock(buffer, 0, buffer, 0);
                                        
                    intTobytes(j + 1, intArray, 0);
                    for (int byteNum = 0; byteNum < BYTES_IN_INTEGER; byteNum++)
                    {
                         buffer[byteNum + blockSize / 2] ^= intArray[byteNum];
                    }

                    Array.Copy(buffer, blockSize / 2, B, 0, blockSize / 2);
                    
                    for (int i = 2; i < n; i++)
                    {                        
                         Array.Copy(bTemp[i - 1], 0, bTemp[i - 2], 0, blockSize / 2);
                    }

                    Array.Copy(buffer, 0, bTemp[n - 2], 0, blockSize / 2);                   
               }


               Array.Copy(B, 0, buffer, 0, blockSize / 2);
               bufOff = blockSize / 2;

               for (int i = 0; i < n - 1; i++)
               {
                    Array.Copy(bTemp[i], 0, buffer, bufOff, blockSize / 2);
                    bufOff += blockSize / 2;
               }

               return buffer;
          }

          public byte[] Unwrap(byte[] input, int inOff, int length)
          {
               if (forWrapping)
               {
                    throw new InvalidOperationException("Not set for unwrapping");
               }

               if ((input.Length - inOff) % blockSize != 0)
               {
                    throw new ArgumentException("Padding not supported");
               }

               int n = 2 * length / blockSize;

               int V = (n - 1) * 6;

               buffer = new byte[input.Length - inOff];
               Array.Copy(input, inOff, buffer, 0, input.Length - inOff);
                              
               byte[] B = new byte[blockSize / 2];
               Array.Copy(buffer, 0, B, 0, blockSize / 2);
              
               List<byte[]> bTemp = new List<byte[]>();

               int bHalfBlocksLen = buffer.Length - blockSize / 2;
               int bufOff = blockSize / 2;
               while (bHalfBlocksLen != 0)
               {
                    byte[] temp = new byte[blockSize / 2];
                    Array.Copy(buffer, bufOff, temp, 0, blockSize / 2);
                  
                    bTemp.Add(temp);

                    bHalfBlocksLen -= blockSize / 2;
                    bufOff += blockSize / 2;     
               }
               
               for (int j = 0; j < V; j++)
               {
                    Array.Copy(bTemp[n - 2], 0, buffer, 0, blockSize / 2);
                    Array.Copy(B, 0, buffer, blockSize / 2, blockSize / 2);
                    intTobytes(V - j, intArray, 0);
                    for (int byteNum = 0; byteNum < BYTES_IN_INTEGER; byteNum++)
                    {
                         buffer[byteNum + blockSize / 2] ^= intArray[byteNum];
                    }
                                      
                    engine.ProcessBlock(buffer, 0, buffer, 0);
              
                    Array.Copy(buffer, 0, B, 0, blockSize / 2);

                    for (int i = 2; i < n; i++)
                    {
                         Array.Copy(bTemp[n - i - 1], 0, bTemp[n - i], 0, blockSize / 2);
                    }

                    Array.Copy(buffer, blockSize / 2, bTemp[0], 0, blockSize / 2);
               }
               
               Array.Copy(B, 0, buffer, 0, blockSize / 2);
               bufOff = blockSize / 2;

               for (int i = 0; i < n - 1; i++)
               {
                    Array.Copy(bTemp[i], 0, buffer, bufOff, blockSize / 2);
                    bufOff += blockSize / 2;
               }

               Array.Copy(buffer, buffer.Length - blockSize, checkSumArray, 0, blockSize);

               if (!Arrays.AreEqual(checkSumArray, zeroArray))
               {
                    throw new InvalidCipherTextException("checksum failed");
               }
               else
               {
                    Array.Resize(ref buffer, buffer.Length - blockSize);
               }

               return buffer;
          }

          //int to array of bytes
          private static void intTobytes(
                    int num,
                    byte[] outBytes,
                    int outOff)
          {
               outBytes[outOff + 3] = (byte)(num >> 24);
               outBytes[outOff + 2] = (byte)(num >> 16);
               outBytes[outOff + 1] = (byte)(num >> 8);
               outBytes[outOff] = (byte)num;
          }
     }
}
