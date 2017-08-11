using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace RainMaker_Net
{
    // 이 클래스는 클라이언트쪽에서 Connect를 수행할때 써준다.
    // EndPoint 정보를 받아서 서버에 접속한다.
    // 접속하려는 서버 하나당 인스턴스 한개씩 생성하여 사용하면 된다.
    public class CConnector
    {
        public delegate void ConnectedHandler(CUserToken token);
        public ConnectedHandler connected_callback { get; set; }

        // 원격지 서버와의 연결을 위한 소켓
        Socket client;

        CNetworkService network_service;

        public CConnector(CNetworkService network_service)
        {
            this.network_service = network_service;
            this.connected_callback = null;
        }

        public void connect(IPEndPoint remote_endpoint)
        {
            this.client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // 비동기 접속을 위한 event args
            SocketAsyncEventArgs event_arg = new SocketAsyncEventArgs();

            event_arg.Completed += on_connect_completed;
            event_arg.RemoteEndPoint = remote_endpoint;

            bool pending = this.client.ConnectAsync(event_arg);

            if(!pending)
            {
                on_connect_completed(null, event_arg);
            }
        }

        void on_connect_completed(object sender, SocketAsyncEventArgs e)
        {
            if(e.SocketError == SocketError.Success)
            {
                CUserToken token = new CUserToken();

                // 데이터 수신 준비.
                this.network_service.on_connect_complete(this.client, token);

                if(this.connected_callback != null)
                {
                    this.connected_callback(token);
                }
            }
            else
            {
                // 실패..
                Console.WriteLine(string.Format("Failed to connect. {0}", e.SocketError));
            }
        }
    }
}
