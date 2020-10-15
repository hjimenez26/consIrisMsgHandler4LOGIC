/*This program listens to named pipes "testpipe" from IRISlistenerConsole and parses incoming ASCII data to be sent out on serial port
 *  -it only works for 2 sensor groups/function areas need to adjust to accomodate variable # of sensors/fa's
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace consIrisMsgHandler4LOGIC
{
    class Program
    {
        public static int numThreads = 8;
        private static SerialPort SerialObj;
        public static string strLog = @"c:\\log\MsgHandler";
        public static string strPortName = "com8";
        static void Main(string[] args)
        {
            Start();
        }
        static void Start()
        {
            try
            {
                SerialObj = new System.IO.Ports.SerialPort(strPortName, 9600, Parity.None, 8, StopBits.One);
                byte[] baInit = new byte[] {0xc6, 0x4c, 0x4f, 0x47, 0x4f, 0x4e};
                SendReceiveData2(baInit);
                Parallel.Invoke(receiveCounts);
            }
            catch (Exception e)
            {
                string strMethodName = MethodBase.GetCurrentMethod().Name;
                string strPathName = strLog + "\\" + strMethodName + ".txt";
                log(e.Message, strPathName);
                Console.WriteLine(e);
            }
        }
        static void receiveCounts()
        {
            Console.WriteLine("receiveCounts");
            try
            {
                while (true)
                {
                    NamedPipeServerStream pipeServer = new NamedPipeServerStream("testpipe", PipeDirection.InOut, numThreads);
                    int threadId = Thread.CurrentThread.ManagedThreadId;
                    pipeServer.WaitForConnection();
                    Console.WriteLine("Client connected on thread[{0}].", threadId);
                    bool boolIsConnected = pipeServer.IsConnected;
                    if (boolIsConnected)
                    {
                        byte[] baASCIIBytes = new byte[45];
                        int intInputSize = pipeServer.InBufferSize;
                        pipeServer.Read(baASCIIBytes, 0, baASCIIBytes.Length);
                        var varASCIIResult = ParseAsciiByte(baASCIIBytes);
                        string strPathName = strLog + "\\" + "path1.txt";
                        if (varASCIIResult.boolIsGood)
                        {
                            byte byteTest = 0x1;
                            if (BitConverter.ToString(varASCIIResult.listBytes[0]) == "01")
                            {
                                if (varASCIIResult.listBytes.Count == 10)
                                {
                                    var varParseListeResult = ParseListFAInOrder10(varASCIIResult.listBytes);
                                    if (varParseListeResult.boolIsGood)
                                    {
                                        SendReceiveData2(varParseListeResult.baFinished);
                                    }
                                }
                                else if (varASCIIResult.listBytes.Count == 5)
                                {
                                    var varParseListeResult = ParseListFAInOrder5(varASCIIResult.listBytes);
                                    if (varParseListeResult.boolIsGood)
                                    {
                                        SendReceiveData2(varParseListeResult.baFinished);
                                    }
                                }
                            }
                            else
                            {
                                if (varASCIIResult.listBytes.Count == 10)
                                {
                                    var varParseListeResult = ParseListFAReversed10(varASCIIResult.listBytes);

                                    if (varParseListeResult.boolIsGood)
                                    {
                                        SendReceiveData2(varParseListeResult.baFinished);
                                    }
                                }
                                else if (varASCIIResult.listBytes.Count == 5)
                                {
                                    var varParseListeResult = ParseListFAReversed5(varASCIIResult.listBytes);
                                    if (varParseListeResult.boolIsGood)
                                    {
                                        SendReceiveData2(varParseListeResult.baFinished);
                                    }

                                }

                            }
                        }

                        log(BitConverter.ToString(baASCIIBytes), strPathName);
                        Console.WriteLine(BitConverter.ToString(baASCIIBytes));
                    }
                    pipeServer.Dispose();
                }//while
            }
            catch (Exception e)
            {
                string strMethodName = MethodBase.GetCurrentMethod().Name;
                string strPathName = strLog + "\\" + strMethodName + ".txt";
                log(e.Message, strPathName);
                Console.WriteLine(e);
            }
        }  //receiveCounts
        static (List<byte[]> listBytes, bool boolIsGood) ParseAsciiByte(byte[] baASCIIBytes)
        {
            List<byte[]> listResults = new List<byte[]>();
            bool boolIsGood = true;
            try
            {   //C5-FF-BF-01-00-BF-02-00-BF-03-00-BF-04-02-34
                int intCposPrior = 0;
                int intCpos = 0;
                int intBegin = 0;
                int intEnd = 0;
                int intSize = 0;
                int intSectionCount = 0;
                int i = 0;
                bool boolDone = false;
                while (i < baASCIIBytes.Length && boolDone == false)
                {

                    if ((baASCIIBytes[i] == 0x2c && intSectionCount == 0))
                    {
                        intCposPrior = intCpos;
                        intCpos = i;
                        intBegin = 0;
                        intEnd = i - 1;
                        intSize = i;
                        byte[] baDest = new byte[intSize];
                        Buffer.BlockCopy(baASCIIBytes, intBegin, baDest, 0, intSize);
                        int intNumber = GetNumber(baDest);
                        byte[] baTemp = GetByte(intNumber);
                        listResults.Add(baTemp);
                        intSectionCount++;

                    }
                    else
                    if ((baASCIIBytes[i] == 0x2c && intSectionCount > 0) || i == (baASCIIBytes.Length - 1))
                    {
                        intCposPrior = intCpos;
                        intCpos = i;
                        intBegin = intCposPrior + 1;
                        intEnd = intCpos - 1;
                        intSize = (intEnd - intBegin) + 1;
                        byte[] baDest = new byte[intSize];
                        Buffer.BlockCopy(baASCIIBytes, intBegin, baDest, 0, intSize);
                        int intNumber = GetNumber(baDest);
                        byte[] baTemp = GetByte(intNumber);
                        listResults.Add(baTemp);
                        intSectionCount++;
                    }
                    else if (baASCIIBytes[i] == 0x0A)
                    {
                        baASCIIBytes[i + 1] = 0x0D;
                        boolDone = true;
                    }
                    i++;
                }//while
                Console.WriteLine("listSize" + listResults.Count);
            }
            catch (Exception e)
            {
                boolIsGood = false;
                string strMethodName = MethodBase.GetCurrentMethod().Name;
                string strPathName = strLog + "\\" + strMethodName + ".txt";
                log(e.Message, strPathName);
                Console.WriteLine(e);
            }
            return (listResults, boolIsGood);
        }
        /// combines all bytes into sendable array, this one is used when the function area was received in desc order
        /// </summary>
        /// <param name="listBA"></param>
        /// <returns></returns>
        static (bool boolIsGood, byte[] baFinished) ParseListFAReversed5(List<byte[]> listBA)
        {
            bool boolIsGood = true;
            byte[] baAnswer = new byte[10];
            try
            {
                //C5-FF-BF-01-00-BF-02-00-

                byte[] ba1 = new byte[] { 0xc5, 0xff, 0xbf, 0x01 };
                byte[] ba2 = new byte[] { 0xbf, 0x02 };
                Buffer.BlockCopy(ba1, 0, baAnswer, 0, 4);
                Buffer.BlockCopy(listBA[2], 0, baAnswer, 4, 1);//offs
                Buffer.BlockCopy(ba2, 0, baAnswer, 5, 2);
                Buffer.BlockCopy(listBA[3], 0, baAnswer, 7, 1);//ons
            }
            catch (Exception e)
            {
                boolIsGood = false;
                string strMethodName = MethodBase.GetCurrentMethod().Name;
                string strPathName = strLog + "\\" + strMethodName + ".txt";
                log(e.Message, strPathName);
                Console.WriteLine(e);
            }


            return (boolIsGood, baAnswer);
        }
        /// <summary>
        /// combines all bytes into sendable array, this one is used when the function area was received in desc order
        /// </summary>
        /// <param name="listBA"></param>
        /// <returns></returns>
        static (bool boolIsGood, byte[] baFinished) ParseListFAReversed10(List<byte[]> listBA)
        {
            bool boolIsGood = true;
            byte[] baAnswer = new byte[15];

            try
            {
                //C5-FF-BF-01-00-BF-02-00-BF-03-00-BF-04-02-34

                byte[] ba1 = new byte[] { 0xc5, 0xff, 0xbf, 0x01 };
                byte[] ba2 = new byte[] { 0xbf, 0x02 };
                byte[] ba3 = new byte[] { 0xbf, 0x03 };
                byte[] ba4 = new byte[] { 0xbf, 0x04 };

                Buffer.BlockCopy(ba1, 0, baAnswer, 0, 4);
                Buffer.BlockCopy(listBA[7], 0, baAnswer, 4, 1);

                Buffer.BlockCopy(ba2, 0, baAnswer, 5, 2);
                Buffer.BlockCopy(listBA[8], 0, baAnswer, 7, 1);

                Buffer.BlockCopy(ba3, 0, baAnswer, 8, 2);
                Buffer.BlockCopy(listBA[2], 0, baAnswer, 10, 1);

                Buffer.BlockCopy(ba4, 0, baAnswer, 11, 2);
                Buffer.BlockCopy(listBA[3], 0, baAnswer, 13, 1);
            }
            catch (Exception e)
            {
                boolIsGood = false;
                string strMethodName = MethodBase.GetCurrentMethod().Name;
                string strPathName = strLog + "\\" + strMethodName + ".txt";
                log(e.Message, strPathName);
                Console.WriteLine(e);
            }


            return (boolIsGood, baAnswer);
        }
        /// <summary>
        /// combines all bytes into sendable array, this one is used when the function area was received in ascending order
        /// </summary>
        /// <param name="listBA"></param>
        /// <returns></returns>
        static (bool boolIsGood, byte[] baFinished) ParseListFAInOrder5(List<byte[]> listBA)
        {
            bool boolIsGood = true;
            byte[] baAnswer = new byte[10];

            try
            {
                //off-on
                //C5-FF-BF-01-00-BF-02-00-34

                byte[] ba1 = new byte[] { 0xc5, 0xff, 0xbf, 0x01 };
                byte[] ba2 = new byte[] { 0xbf, 0x02 };


                Buffer.BlockCopy(ba1, 0, baAnswer, 0, 4);
                Buffer.BlockCopy(listBA[2], 0, baAnswer, 4, 1);//offs

                Buffer.BlockCopy(ba2, 0, baAnswer, 5, 2);
                Buffer.BlockCopy(listBA[3], 0, baAnswer, 7, 1);//ons

            }
            catch (Exception e)
            {
                boolIsGood = false;
                string strMethodName = MethodBase.GetCurrentMethod().Name;
                string strPathName = strLog + "\\" + strMethodName + ".txt";
                log(e.Message, strPathName);
                Console.WriteLine(e);
            }


            return (boolIsGood, baAnswer);
        }
        /// <summary>
        /// combines all bytes into sendable array, this one is used when the function area was received in ascending order
        /// </summary>
        /// <param name="listBA"></param>
        /// <returns></returns>
        static (bool boolIsGood, byte[] baFinished) ParseListFAInOrder10(List<byte[]> listBA)
        {
            bool boolIsGood = true;
            byte[] baAnswer = new byte[15];

            try
            {
                //C5-FF-BF-01-00-BF-02-00-BF-03-00-BF-04-02-34

                byte[] ba1 = new byte[] { 0xc5, 0xff, 0xbf, 0x01 };
                byte[] ba2 = new byte[] { 0xbf, 0x02 };
                byte[] ba3 = new byte[] { 0xbf, 0x03 };
                byte[] ba4 = new byte[] { 0xbf, 0x04 };

                Buffer.BlockCopy(ba1, 0, baAnswer, 0, 4);
                Buffer.BlockCopy(listBA[2], 0, baAnswer, 4, 1);

                Buffer.BlockCopy(ba2, 0, baAnswer, 5, 2);
                Buffer.BlockCopy(listBA[3], 0, baAnswer, 7, 1);

                Buffer.BlockCopy(ba3, 0, baAnswer, 8, 2);
                Buffer.BlockCopy(listBA[7], 0, baAnswer, 10, 1);

                Buffer.BlockCopy(ba4, 0, baAnswer, 11, 2);
                Buffer.BlockCopy(listBA[8], 0, baAnswer, 13, 1);
            }
            catch (Exception e)
            {
                boolIsGood = false;
                string strMethodName = MethodBase.GetCurrentMethod().Name;
                string strPathName = strLog + "\\" + strMethodName + ".txt";
                log(e.Message, strPathName);
                Console.WriteLine(e);
            }


            return (boolIsGood, baAnswer);
        }


        static public (bool boolIsGood, Exception exc) SendReceiveData2(byte[] byteArryData)
        {
            string strMethodName = MethodBase.GetCurrentMethod().Name;
            string strPathName = strLog + "\\" + strMethodName + ".txt";
            byte[] inbyte = { 0 };
            // log("outgoing: " + BitConverter.ToString(byteArryData), strPathName);
            bool boolIsGood = true;
            Exception ee = null;
            try
            {
                SerialObj.Encoding = Encoding.UTF8;
                SerialObj.DiscardNull = false;
                if (SerialObj.IsOpen == false)
                {
                    SerialObj.Open();
                }
                {
                    SerialObj.Write(byteArryData, 0, byteArryData.Length);
                }
                SerialObj.RtsEnable = false;
                CommTimer tmrComm = new CommTimer();
                tmrComm.Start(20000);
                SerialObj.DiscardOutBuffer();
            }//try
            catch (Exception exc)
            {
                ee = exc;
                strPathName = strLog + "\\" + strMethodName + ".txt";
                log(exc.Message, strPathName);
                Console.WriteLine(exc);
                boolIsGood = false;
            }
            return (boolIsGood, ee);
        }

        static byte[] GetByte(int intInput)
        {
            byte[] baByte = new byte[1];
            try
            {
                baByte[0] = Convert.ToByte(intInput);
            }
            catch (Exception e)
            {
                string strMethodName = MethodBase.GetCurrentMethod().Name;
                string strPathName = strLog + "\\" + strMethodName + ".txt";
                log(e.Message, strPathName);
                Console.WriteLine(e);
            }
            return baByte;
        }

        static int GetNumber(byte[] baBytes)
        {
            int intResult = 0;
            try
            {
                string strNumber = Encoding.Default.GetString(baBytes);
                //  Console.WriteLine(strNumber);
                int.TryParse(strNumber, out intResult);
                Console.WriteLine(intResult);
            }
            catch (Exception e)
            {
                string strMethodName = MethodBase.GetCurrentMethod().Name;
                string strPathName = strLog + "\\" + strMethodName + ".txt";
                log(e.Message, strPathName);
                Console.WriteLine(e);
            }
            return intResult;
        }
        public class CommTimer
        {
            public System.Timers.Timer tmrComm = new System.Timers.Timer();
            public bool timedout = false;
            public CommTimer()
            {
                timedout = false;
                tmrComm.AutoReset = false;
                tmrComm.Enabled = false;
                tmrComm.Interval = 2000; //default to 1 second
                tmrComm.Elapsed += new ElapsedEventHandler(OnTimedCommEvent);
            }
            public void OnTimedCommEvent(object source, ElapsedEventArgs e)
            {
                timedout = true;
                tmrComm.Stop();
            }
            public void Start(double timeoutperiod)
            {
                tmrComm.Interval = timeoutperiod;             //time to time out in milliseconds
                tmrComm.Stop();
                timedout = false;
                tmrComm.Start();
            }
        }//*****class CommTimer****
        public static void log(string strContent, string strFileName)
        {
            try
            {
                using (StreamWriter w = File.AppendText(strFileName))
                {
                    w.Write("\r\nLog Entry : ");
                    w.WriteLine("{0} {1}", DateTime.Now.ToLongTimeString(),
                        DateTime.Now.ToLongDateString());
                    w.WriteLine(" :");
                    w.WriteLine("text:" + strContent);
                    w.WriteLine("-------------------------------");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                
            }
        } //***log***
    }
}

