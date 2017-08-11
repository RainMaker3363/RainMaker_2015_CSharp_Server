using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;

namespace RainMaker_Net
{
    public class CNetworkService
    {
        int connected_count;
        // 클라이언트의 접속을 받아들이기 위한 객체
        CListener client_listener;

        // 메시지 송, 수신시 필요한 객체
        SocketAsyncEventArgsPool receive_event_args_pool;
        SocketAsyncEventArgsPool send_event_args_pool;

        // 메시지 수신, 전송시 .Net 비동기 소켓에서 사용할 버퍼를 관리하는 객체입니다.
        BufferManager buffer_manager;

        // 클라이언트 접속시 호출되는 델리게이트
        public delegate void SessionHandler(CUserToken token);
        public SessionHandler sessing_created_callback { get; set; }

        // 설정 값들..
        int max_connections;
        int buffer_size;

        // 읽기, 쓰기
        readonly int pre_alloc_count = 2;

        public CNetworkService()
        {
            this.connected_count = 0;
            this.sessing_created_callback = null;
        }

        public void Initialize()
        {
            this.max_connections = 10000;
            this.buffer_size = 1024;

            // 버퍼의 전체 크기는 아래 공식으로 계산됩니다.
            // 버퍼의 전체 크기 = 최대 동접 수치 * 버퍼 하나의 크기 * (전소용, 수신용)
            // 전송용 한개, 수신용 한개 총 두개가 필요하기 때문에 pre_alloc_count = 2로 설정해 놨습니다.
            this.buffer_manager = new BufferManager(this.max_connections * this.buffer_size * this.pre_alloc_count, this.buffer_size);

            // 최대 동접 수치만큼 생성합니다.
            this.receive_event_args_pool = new SocketAsyncEventArgsPool(this.max_connections);
            this.send_event_args_pool = new SocketAsyncEventArgsPool(this.max_connections);

            // SocketAsyncEvnetArgs 오브젝트의 풀을 초기화 시킨다.
            this.buffer_manager.InitBuffer();

            SocketAsyncEventArgs arg;

            for(int i = 0; i<this.max_connections; i++)
            {
                // 동일한 소켓에 대고 send, receive를 하므로
                // user token은 세션별로 하나씩만 만들어 놓고
                // receive, send EventArgs에서 동일한 token을 참조하도록 구성합니다.
                CUserToken token = new CUserToken();

                // 수신 풀(Receive Pool)
                {
                    arg = new SocketAsyncEventArgs();
                    arg.Completed += new EventHandler<SocketAsyncEventArgs>(receive_completed);
                    arg.UserToken = token;

                    // SocketAsyncEventArg 버퍼 풀에 바이트 버퍼를 할당한다.
                    this.buffer_manager.SetBuffer(arg);

                    // SocketAsyncEventArg 객체를 풀에 추가해준다.
                    this.receive_event_args_pool.Push(arg);
                }

                // 송신 풀(Send Pool)
                {
                    arg = new SocketAsyncEventArgs();
                    arg.Completed += new EventHandler<SocketAsyncEventArgs>(send_completed);
                    arg.UserToken = token;

                    this.buffer_manager.SetBuffer(arg);

                    this.send_event_args_pool.Push(arg);
                }
            }
        }

        // 서버의 host, port, backlog 값을 전달 받아 listenere을 생성한 뒤 start메소드로 접속을 기다립니다.
        public void listen(string host, int port, int backlog)
        {
            this.client_listener = new CListener();
            this.client_listener.callback_on_newclient += on_new_client;
            this.client_listener.start(host, port, backlog);
        }

        // 새로운 클라이언트가 접속 성공 했을 때 호출됩니다.
        // AcceptAsync의 콜백 메소드에서 호출되며 여러 스레드에서 동시에 호출될 수 있기 때문입니다.
        void on_new_client(Socket client_socket, object token)
        {
            Interlocked.Increment(ref this.connected_count);

            Console.WriteLine(string.Format("[{0}] A client connected. handle {1},  count {2}",
            Thread.CurrentThread.ManagedThreadId, client_socket.Handle,
            this.connected_count));

            // 풀에서 하나 꺼내와 사용한다.
            SocketAsyncEventArgs receive_args = this.receive_event_args_pool.Pop();
            SocketAsyncEventArgs send_args = this.send_event_args_pool.Pop();

            CUserToken user_token = null;

            // SocketAsyncEventArgs를 생성할 때 만들어 두었던 CUserToken을 꺼내와서
            // 콜백 메소드의 파라미터로 넘겨줍니다.
            if(this.sessing_created_callback != null)
            {
                user_token = receive_args.UserToken as CUserToken;
                this.sessing_created_callback(user_token);
            }

            // 이제 클라이언트로부터 데이터를 수신할 준비를 합니다.
            begin_receive(client_socket, receive_args, send_args);
        }

        // 클라이언트가 접속한 이유는 무언가를 주고 받기 위함이니 이제 메시지 수신을 위한 작업을 시작합니다.
        // 전송을 위한 작업은 서버에서 메시지를 보낼 시점에 일어나므로 아직 준비할 필요는 없습니다.
        void begin_receive(Socket socket, SocketAsyncEventArgs receive_args, SocketAsyncEventArgs send_args)
        {
            // receive_args, send_args 아무곳에서나 꺼내와도 된다. 둘 다 동일한 CUserToken을 사용하기 때문
            CUserToken token = receive_args.UserToken as CUserToken;

            token.set_event_args(receive_args, send_args);

            // 새로 생선된 클라이언트 소켓을 보관해 놓고 통신할 때 사용한다.
            token.socket = socket;

            // 데이터를 받을 수 있도록 소켓 메소드를 호출해준다.
            // 비동기로 수신할 경우 워커 스레드에서 대기중으로 있다가 Completed에 설정해놓은 메소드가 호출된다.
            // 동기로 완료될 경우에는 직접 완료 메소드를 호출해줘야 한다.
            bool pending = socket.ReceiveAsync(receive_args);

            if(!pending)
            {
                process_receive(receive_args);
            }
        }

        // 원격 서버에 접속 성공했을 때 호출
        public void on_connect_complete(Socket socket, CUserToken token)
        {
            // SocketAsyncEventArgsPool에서 빼오지 않고 그때 그때 할당해서 사용한다.
            // 풀은 서버에서 클라이언트와의 통신용으로만 쓰려고 만든것이기 때문이다.
            // 클라이언트 입장에서 서버와 통신을 할 때는 접속한 서버당 두개의 EventArgs만 있으면 되기 때문에 그냥 new해서 쓴다
            // 서버간 연결에서도 마찬가지이다.
            // 풀링처리를 하려면 c->s로 가는 별도의 풀을 만들어서 써야 한다.

            SocketAsyncEventArgs receive_event_arg = new SocketAsyncEventArgs();
            receive_event_arg.Completed += new EventHandler<SocketAsyncEventArgs>(receive_completed);
            receive_event_arg.UserToken = token;
            receive_event_arg.SetBuffer(new byte[1024], 0, 1024);

            SocketAsyncEventArgs send_event_arg = new SocketAsyncEventArgs();
            send_event_arg.Completed += new EventHandler<SocketAsyncEventArgs>(send_completed);
            send_event_arg.UserToken = token;
            send_event_arg.SetBuffer(new byte[1024], 0, 1024);

            begin_receive(socket, receive_event_arg, send_event_arg);
        }

        // Receive가 완료 됬을때 호출되는 메소드
        // 오류가 났을때 예외를 던져줍니다.
        void receive_completed(object sender, SocketAsyncEventArgs e)
        {
            if(e.LastOperation == SocketAsyncOperation.Receive)
            {
                process_receive(e);
                return;
            }

            throw new ArgumentException("The Last operation completed on the socket was not a receive.");
        }

        // Send가 완료 됬을때 호출되는 메소드.
        void send_completed(object sender, SocketAsyncEventArgs e)
        {
            CUserToken token = e.UserToken as CUserToken;
            token.process_send(e);
        }

        // 이 메소드는 비동기적으로 Receive를 완수했을때 호출되는 메소드입니다./
        // 호스트(Host)의 연결이 끊키면 소켓은 자동적으로 Close 됩니다.
        private void process_receive(SocketAsyncEventArgs e)
        {
            // 원격 호스트의 연결이 끊켰는지 확인한다.
            CUserToken token = e.UserToken as CUserToken;

            if(e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
            {
                // 이후의 작업은 CUserToken에 맡긴다.
                // e.Buffer에는 클라이언트로부터 수신된 데이터들이 들어있습니다.
                // e.Offset은 수신된 버퍼의 포지션입니다.
                // e.BytesTransferred는 이번에 수신된 데이터의 바이트 수를 나타냅니다.
                token.on_receive(e.Buffer, e.Offset, e.BytesTransferred);

                // 다음 메시지 수신을 위해서 다시 ReceiveAsync메소드를 호출한다.
                bool pending = token.socket.ReceiveAsync(e);
                if(!pending)
                {
                    process_receive(e);
                }
            }
            else
            {
                Console.WriteLine(string.Format("error {0}, transferred {1}", e.SocketError, e.BytesTransferred));
                close_clientsocket(token);
            }
        }

        public void close_clientsocket(CUserToken token)
        {
            token.on_removed();

            // 버퍼는 반환할 필요가 없다. SocketAsyncEventArg가 버퍼를 물고 있기 때문에.
            // 이것을 재사용 할 때 물고 있는 버퍼를 그대로 사용하면 되기 때문이다.
            if(this.receive_event_args_pool != null)
            {
                this.receive_event_args_pool.Push(token.receive_event_args);
            }

            if(this.send_event_args_pool != null)
            {
                this.send_event_args_pool.Push(token.send_event_args);
            }
        }
    }
}
