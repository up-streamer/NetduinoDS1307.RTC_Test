using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using netduino.helpers.Hardware; // DS1307 drive NameSpace
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.Netduino;
using System;
using System.Diagnostics; //Added for use Debug.Print
using System.IO; //Added for NTP synch tests
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace NetduinoDS1307.RTC_Test
{
    public class Program
    {
        public static void Main()
        {
            var clk = new CLK { };

            var synchThread = new Thread(clk.synch);
            var showThread = new Thread(clk.show);

            synchThread.Start();
            showThread.Start();
        }

        public class CLK
        {
            public void synch()
            {
                var RTC_Clock = new DS1307();

                //RTC_Clock.Set(new DateTime(2018, 6, 18, 20, 00, 00)); // (year, month, day, hour, minute, second)
                
                RTC_Clock.Halt(false);  /* To make shure RTC is running */

                while (true)
                {
                    try
                    {
                        DateTime DateTimeNTP = NTPTime("pool.ntp.org", -180);

                        if (!(DateTimeNTP.Year == 1900))
                        {
                            // Synch RTC nd system clock from NTPTime
                            Debug.Print("Synch OK!");
                            Debug.Print("Internet Time " + DateTimeNTP.ToString());
                            Debug.Print("System Clock  Before Synch " + DateTime.Now.ToString());
                            Utility.SetLocalTime(DateTimeNTP);
                            Debug.Print("System Clock After Synch " + DateTime.Now.ToString());

                            //Debug.Print("RTC Before " + RTC_Clock.Get().ToString()); 
                            RTC_Clock.Halt(true);
                            RTC_Clock.Set(DateTimeNTP);
                            RTC_Clock.Halt(false);
                            Debug.Print("RTC After Synch " + RTC_Clock.Get().ToString());
                        }
                        else
                        {
                            Debug.Print("NTP socket failure!  Check Internet connection");
                            Debug.Print("Synch system Clock using RTC");
                            Utility.SetLocalTime(RTC_Clock.Get());
                            Debug.Print("System Clock " + DateTime.Now.ToString());
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.Print(ex.ToString());
                        Debug.Print("General Failure!  Verify internet connection and RTC");
                    }
                    /* sleep for 1 minute */
                    Thread.Sleep(60000);
                }
            }
            public void show()
            {
                while (true)
                {
                    Debug.Print(DateTime.Now.ToString());
                    Thread.Sleep(1000);
                }
            }
            /// <summary>
            /// *** Original code from Michael Schwarz blog ***
            /// https://weblogs.asp.net/mschwarz/wrong-datetime-on-net-micro-framework-devices
            /// Also found on NeonMika webserver project
            /// Try to get date and time from the NTP server
            /// </summary>
            /// <param name="TimeServer">Time server to use, ex: pool.ntp.org</param>
            /// <param name="GmtOffset">GMT offset in minutes, ex: -240</param>
            /// <returns>Returns true if successful</returns>

            public static DateTime NTPTime(string TimeServer, int GmtOffset)
            {
                Socket s = null;
                DateTime resultTime = new DateTime(1900, 1, 1);
                try
                {
                    EndPoint rep = new IPEndPoint(Dns.GetHostEntry(TimeServer).AddressList[0], 123);
                    s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    byte[] ntpData = new byte[48];
                    Array.Clear(ntpData, 0, 48);
                    ntpData[0] = 0x1B; // Set protocol version
                    s.SendTo(ntpData, rep); // Send Request   
                    if (s.Poll(30 * 1000 * 1000, SelectMode.SelectRead)) // Waiting an answer for 30s, if nothing: timeout
                                                                         // Change to 3s for fast debbugging
                    {
                        s.ReceiveFrom(ntpData, ref rep); // Receive Time
                        byte offsetTransmitTime = 40;
                        ulong intpart = 0;
                        ulong fractpart = 0;
                        for (int i = 0; i <= 3; i++) intpart = (intpart << 8) | ntpData[offsetTransmitTime + i];
                        for (int i = 4; i <= 7; i++) fractpart = (fractpart << 8) | ntpData[offsetTransmitTime + i];
                        ulong milliseconds = (intpart * 1000 + (fractpart * 1000) / 0x100000000L);
                        s.Close();
                        resultTime += TimeSpan.FromTicks((long)milliseconds * TimeSpan.TicksPerMillisecond);
                        resultTime = resultTime.AddMinutes(GmtOffset);
                    }
                    s.Close();
                }
                catch
                {
                    try { s.Close(); }
                    catch { }
                }
                return resultTime;
            }
        }
    }
}