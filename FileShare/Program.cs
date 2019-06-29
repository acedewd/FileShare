using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using CommandLine;

namespace FileShare
{
    class Options
    {
        [Option('f')]
        public bool IsSendMode { get; set; }

        [Option('p', "port", Required = true)]
        public int Port { get; set; }

        [Option('h', "host", Required = false)]
        public string Host { get; set; }

        [Option('d', "path", Required = true)]
        public string Path { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Parser.Default
                .ParseArguments<Options>(args)
                .WithParsed(o =>
                {
                    var ip = IPAddress.Parse(o.Host);

                    if (o.IsSendMode)
                    {
                        using (var socket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
                        {
                            var endpoint = new IPEndPoint(ip, o.Port);
                            socket.Connect(endpoint);

                            var fileInfo = new FileInfo(o.Path);
                            socket.Send(BitConverter.GetBytes(fileInfo.Length));

                            var buffer = new byte[1024];
                            using (var fileStream = File.OpenRead(o.Path))
                            {
                                int bytesRead;
                                var bytesWritten = 0;
                                while ((bytesRead = fileStream.Read(buffer)) > 0)
                                {
                                    socket.Send(buffer, 0, bytesRead, SocketFlags.None);
                                    bytesWritten += bytesRead;
                                    Console.WriteLine($"Sent {bytesWritten}/{fileInfo.Length} bytes");
                                }
                            }
                        }
                    }
                    else
                    {
                        using (var socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp))
                        {
                            var ipHostInfo = Dns.GetHostEntry("localhost");
                            var ipAddress = ipHostInfo.AddressList[0];
                            var localEndPoint = new IPEndPoint(ipAddress, o.Port);

                            socket.Bind(localEndPoint);
                            socket.Listen(1);

                            var handler = socket.Accept();
                            Console.WriteLine($"Connected to {handler.RemoteEndPoint}");

                            var buffer = new byte[1024];
                            handler.Receive(buffer, sizeof(long), SocketFlags.None);

                            var fileSize = BitConverter.ToInt32(buffer, 0);
                            Console.WriteLine($"File size {fileSize} bytes");
                            
                            using (var fileStream = File.OpenWrite(o.Path))
                            {
                                var bytesWritten = 0;
                                while (bytesWritten < fileSize)
                                {
                                    var bytesRead = handler.Receive(buffer);
                                    fileStream.Write(buffer, 0, bytesRead);
                                    bytesWritten += bytesRead;
                                    Console.WriteLine($"Received {bytesWritten}/{fileSize} bytes");
                                }
                            }
                        }
                    }
                });
        }
    }
}
