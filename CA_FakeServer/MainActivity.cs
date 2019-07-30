﻿using DLL_Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Linq;
using System.IO;

namespace CA_FakeServer
{
    public class MainActivity
    {
        private Serilog.Core.Logger _logger = Logger.GetInstance();

        public int LocalPort { get; private set; }
        public string LocalIP { get; private set; }

        private Socket _listener;

        private bool _keepRunning;

        private bool _keepTransmitting;

        public int MainLoopTimeSleeping { get; private set; }

        private byte[] _header = new byte[24];
        private List<byte[]> _packets = new List<byte[]>();

        public MainActivity()
        {
            LocalPort = 1717;
            LocalIP = "127.0.0.1";
            MainLoopTimeSleeping = 1;
        }

        public void Stop()
        {
            _keepTransmitting = false;
            _keepRunning = false;
        }

        public void Run()
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
                        
            try
            {
                _logger.Information(string.Format("Main Action Starting  ..."));

                // Building up the main header to transmit
                _header[0] = (byte) 1;
                _header[1] = (byte) 24;

                byte[] pid = BitConverter.GetBytes(15324);
                byte[] rH = BitConverter.GetBytes(1920);
                byte[] rW = BitConverter.GetBytes(1080);
                byte[] vH = BitConverter.GetBytes(480);
                byte[] vW = BitConverter.GetBytes(270);

                _header[2] = pid[0];
                _header[3] = pid[1];
                _header[4] = pid[2];
                _header[5] = pid[3];

                _header[6] = rH[0];
                _header[7] = rH[1];
                _header[8] = rH[2];
                _header[9] = rH[3];

                _header[10] = rW[0];
                _header[11] = rW[1];
                _header[12] = rW[2];
                _header[13] = rW[3];

                _header[14] = vH[0];
                _header[15] = vH[1];
                _header[16] = vH[2];
                _header[17] = vH[3];

                _header[18] = vW[0];
                _header[19] = vW[1];
                _header[20] = vW[2];
                _header[21] = vW[3];

                _header[22] = (byte) 1;
                _header[23] = (byte) 2;

                // Acquiring data to transmit
                _packets = ImageExtraction();
                // TO DO

                IPAddress localAddr = IPAddress.Parse(LocalIP);
                IPEndPoint localEndPoint = new IPEndPoint(localAddr, LocalPort);
                _listener = new Socket(localAddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                _logger.Information("Socket {0}:{1} initialized", LocalIP, LocalPort);
                _listener.Bind(localEndPoint);
                _logger.Information("Socket {0}:{1} bounded", LocalIP, LocalPort);
                _listener.Listen(1);

                //Loop Main Activity until Stop is set
                _keepRunning = true;
                Console.CancelKeyPress += new ConsoleCancelEventHandler(Console_CancelKeyPress);
                while (_keepRunning)
                {
                    _logger.Information("Socket {0}:{1} is listening for connections", LocalIP, LocalPort);
                    Socket _handler = _listener.Accept();

                    IPEndPoint remoteIpEndPoint = _handler.RemoteEndPoint as IPEndPoint;
                    string remoteIP = remoteIpEndPoint.Address.ToString();
                    int remotePort = remoteIpEndPoint.Port;

                    try
                    {
                        _logger.Information("A connection on socket {0}:{1} has been established", remoteIP, remotePort);
                        _keepTransmitting = true;

                        _logger.Information("Starting trasmission on socket {0}:{1} ...", remoteIP, remotePort);
                        // Transmit just the header
                        _handler.Send(_header);
                        _logger.Information("Header sent on socket {0}:{1}", remoteIP, remotePort);
                        while (_keepTransmitting)
                        {
                            //Transmit all packets
                            _logger.Debug("Data in continous trasmission on socket {0}:{1}", remoteIP, remotePort);
                            if (_packets == null || _packets.Count < 1)
                                break;

                            foreach (byte[] packet in _packets)
                            {
                                int len = _handler.Send(packet);
                                Thread.Sleep(MainLoopTimeSleeping);
                            }
                        }                        
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, string.Format("Handler Socket {0}:{1} failure! Message: {2}", remoteIP, remotePort, ex.Message));
                    }
                    finally
                    {
                        _keepTransmitting = false;
                        _logger.Information("Trasmission on socket {0}:{1} terminated", remoteIP, remotePort);

                        if (_handler != null)
                        {
                            _logger.Information("Shutting down TCP socket {0}:{1} ..", remoteIP, remotePort);
                            _handler.Shutdown(SocketShutdown.Send);
                            _logger.Information("Socket shut down");

                            _logger.Information("Closing TCP socket {0}:{1} ..", remoteIP, remotePort);
                            _handler.Close();
                            _logger.Information("Socket closed");
                        }
                    }

                    Thread.Sleep(MainLoopTimeSleeping);
                }
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if (_listener != null)
                {                    
                    try
                    {
                        _logger.Information("Shutting down TCP socket {0}:{1} ..", LocalIP, LocalPort);
                        _listener.Shutdown(SocketShutdown.Both);
                        _logger.Information("Socket shut down");
                    }
                    catch (Exception) { }

                    _logger.Information("Closing TCP socket {0}:{1} ..", LocalIP, LocalPort);
                    _listener.Close();
                    _logger.Information("Socket closed");
                }

                stopwatch.Stop();
                _logger.Information(string.Format("Main Action Completed in {0}", Utils.ElapsedTime(stopwatch.Elapsed)));
            }
        }

        private void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            Stop();
            _logger.Information(string.Format("Cancelling Main Action Execution ..."));
        }

        public void Test()
        {
            ImageExtraction();
        }

        private List<byte[]> ImageExtraction()
        {
            string startupPath = System.IO.Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.Parent.FullName;
            string inputPath = "Input";
            string inputFile = "video.mp4";
            string input_fullfilename = System.IO.Path.Combine(startupPath, inputPath, inputFile);

            string programPath = System.IO.Path.Combine(System.IO.Path.GetPathRoot(Environment.SystemDirectory), "ffmpeg", "bin");
            string programName = "ffmpeg.exe";

            uint w = 108;
            uint h = 192;

            List<byte[]> images = new List<byte[]>();

            Process process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = System.IO.Path.Combine(programPath, programName),
                    Arguments = string.Join(" ", new List<string>() { "-i", input_fullfilename, "-c:v", "mjpeg", "-f", "image2pipe", "-s", string.Format("{0}x{1}", h, w), "pipe:1" }),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                }
            };
            process.Start();
            byte[] data_byte = new byte[]{ };
            BinaryReader br = new BinaryReader(process.StandardOutput.BaseStream);
            process.WaitForExit();
            process.Close();
            int len = 0;

            while (!process.StandardOutput.EndOfStream)
            {
                byte[] bytes_line = { };
                len += br.Read(bytes_line);
            }                
            process.WaitForExit();
            process.Close();

            if (data_byte == null || data_byte.Length == 0)
                return images;            

            //data_byte = new byte[] { 0x00, 0x00, 0x23, 0x45, 0x67, 0xFF, 0xD8, 0x00, 0x00, 0x23, 0x45, 0x67, 0x00, 0x00, 0x23, 0x45, 0x67, 0x00, 0x00, 0x23, 0x45, 0x67, 0xFF, 0xD9, 0x00, 0x00, 0x23, 0x45, 0x67, 0xFF, 0xD8, 0x00, 0x00, 0x23, 0x45, 0x67, 0x00, 0x00, 0x23, 0x45, 0x67, 0x00, 0x00, 0x23, 0x45, 0x67, 0xFF, 0xD9, 0x00, 0x00, 0x23, 0x45, 0x67, 0xFF, 0xD8, 0x00, 0x00, 0x23, 0x45, 0x67, 0x00, 0x00, 0x23, 0x45, 0x67, 0x00, 0x00, 0x23, 0x45, 0x67, 0xFF, 0xD9, 0x00, 0x00, 0x23, 0x45, 0x67, 0xFF, 0xD8, 0x00, 0x00, 0x23, 0x45, 0x67, 0x00, 0x00, 0x23, 0x45, 0x67, 0x00, 0x00, 0x23, 0x45, 0x67, 0xFF, 0xD9, 0x00, 0x00, 0x23, 0x45, 0x67, 0xFF, 0xD8, 0x00, 0x00, 0x23, 0x45, 0x67, 0x00, 0x00, 0x23, 0x45, 0x67, 0x00, 0x00, 0x23, 0x45, 0x67, 0xFF, 0xD9, 0x00, 0x00, 0x23, 0x45, 0x67, 0xFF, 0xD8, 0x00, 0x00, 0x23, 0x45, 0x67, 0x00, 0x00, 0x23, 0x45, 0x67, 0x00, 0x00, 0x23, 0x45, 0x67, 0xFF, 0xD9, 0x00, 0x00, 0x23, 0x45, 0x67, 0xFF, 0xD8, 0x00, 0x00, 0x23, 0x45, 0x67, 0x00, 0x00, 0x23, 0x45, 0x67, 0x00, 0x00, 0x23, 0x45, 0x67, 0xFF, 0xD9, 0x00, 0x00, 0x23, 0x45, 0x67, 0xFF, 0xD8, 0x00, 0x00, 0x23, 0x45, 0x67, 0x00, 0x00, 0x23, 0x45, 0x67, 0x00, 0x00, 0x23, 0x45, 0x67, 0xFF, 0xD9, 0x00, 0x00, 0x23, 0x45, 0x67, 0xFF, 0xD8, 0x00, 0x00, 0x23, 0x45, 0x67, 0x00, 0x00, 0x23, 0x45, 0x67, 0x00, 0x00, 0x23, 0x45, 0x67, 0xFF, 0xD9 };

            // Gather an array of JPGs
            // A JPG is delimited by 2 Sequences:
            // SOI (Start of Image) 0xFF 0xD8
            // EOI (End of Image)   0xFF 0xD9

            byte[] soi_pattern = { 0xFF, 0xD8 };
            int[] start_indexes = (from index in Enumerable.Range(0, data_byte.Length - soi_pattern.Length + 1)
                                   where data_byte.Skip(index).Take(soi_pattern.Length).SequenceEqual(soi_pattern)
                                   select (int)index).ToArray();

            byte[] eoi_pattern = { 0xFF, 0xD9 };
            int[] end_indexes = (from index in Enumerable.Range(0, data_byte.Length - eoi_pattern.Length + 1)
                                 where data_byte.Skip(index).Take(eoi_pattern.Length).SequenceEqual(eoi_pattern)
                                 select (int)index).ToArray();

            for (int i = 0; i < start_indexes.Count(); i++)
            {
                int start_index = start_indexes[i];
                int end_index = end_indexes[i];

                byte[] image = data_byte.Skip(start_index).Take(end_index - start_index).ToArray();
                images.Add(image);
            }

            return images;
        }
    }
}
