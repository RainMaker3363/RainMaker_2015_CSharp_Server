using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;


namespace RainMaker_Net
{
    public class CUserToken
    {
        public Socket socket { get; set; }

        public SocketAsyncEventArgs receive_event_args { get; private set; }
        public SocketAsyncEventArgs send_event_args { get; private set; }

        // 바이트를 패킷 형식으로 해석해준다.
        CMessageResolver message_resolver;

        // Session 객체. 어플리케이션 딴에서 구현하여 사용
        IPeer peer;

        // 전송할 패킷을 보관해놓는 큐. 1- send로 처리하기 위한 큐이다.
        Queue<CPacket> sending_queue;

        // sending_queue lock 처리에 사용되는 객체
        private object cs_sending_queue;

        public CUserToken()
        {
            this.cs_sending_queue = new object();
            this.message_resolver = new CMessageResolver();

            this.peer = null;
            this.sending_queue = new Queue<CPacket>();
        }

        public void set_peer(IPeer peer)
        {
            this.peer = peer;
        }

        public void set_event_args(SocketAsyncEventArgs receive_event_args, SocketAsyncEventArgs send_event_args)
        {
            this.receive_event_args = receive_event_args;
            this.send_event_args = send_event_args;
        }

        // 이 메소드에서 직접 바이트 데이터를 해석해도 되지만 Message Resolver 클래스를 따로 둔 이유는
        // 추후에 확장성을 고려하여 다른 resolver를 구현할 때 CUserToken 클래스의 코드 수정을 위해서다.
        public void on_receive(byte[] buffer, int offset, int transfered)
        {
            this.message_resolver.on_receive(buffer, offset, transfered, on_meesage);
        }

        // Message Resolver로부터 메시지가 해석된 후 호출되는 콜백이다.
        void on_meesage(Const<byte[]> buffer)
        {
            if(this.peer != null)
            {
                this.peer.on_message(buffer);
            }
        }

        public void on_removed()
        {
            this.sending_queue.Clear();

            if(this.peer != null)
            {
                this.peer.on_removed();
            }
        }
        
        // 패킷을 전송한다.
        // 큐가 비어있을 경우에는 큐에 추가한 뒤 바로 SendAsync메소드를 호출하고,
        // 데이터가 들어있을 경우에는 새로 추가만 한다.
        //
        // 큐잉된 패킷의 전송 시점 :
        //                           현재 진행중인 SendAsync가 완료되었을 때 큐를 검사하여 나머지 패킷을 전송한다.
        public void send(CPacket msg)
        {
            CPacket clone = new CPacket();

            msg.copy_to(clone);

            lock(this.cs_sending_queue)
            {
                // 큐가 비어있다면 큐에 추가하고 바로 비동기 전송 메소드를 호출한다.
                if(this.sending_queue.Count <= 0)
                {
                    this.sending_queue.Enqueue(clone);
                    start_send();
                    return;
                }

                // 큐에 무언가가 들어 있다면 아직 이전 전송이 완료되지 않는 상태이므로 큐에 추가만 하고 리턴한다.
                // 현재 수행중인 SendAsync가 완료된 이후에 큐를 검사하여 데이터가 있으면 SendAsync를 호출하여 전송해 준다.
                Console.WriteLine("Queue is not empty. Copy and Enqueue a Msg, protocol id : " + msg.protocol_id);
                this.sending_queue.Enqueue(clone);
            }
        }

        // 비동기 전송을 시작한다.
        void start_send()
        {
            lock(this.cs_sending_queue)
            {
                // 전송이 아직 완료된 상태가 아니므로 데이터만 가져오고 큐에서 제거하진 않는다.
                CPacket msg = this.sending_queue.Peek();

                // 헤더에 패킷 사이즈를 기록한다.
                msg.record_size();

                // 이번에 보낼 패킷 사이즈 만큼 버퍼 크기를 설정하고
                this.send_event_args.SetBuffer(this.send_event_args.Buffer, this.send_event_args.Offset, msg.position);

                // 패킷 내용을 SocketAsyncEventArgs버퍼에 복사한다.
                Array.Copy(msg.buffer, 0, this.send_event_args.Buffer, this.send_event_args.Offset, msg.position);

                // 비동기 전송 시작
                bool pending = this.socket.SendAsync(this.send_event_args);

                if(!pending)
                {
                    process_send(this.send_event_args);
                }
            }
        }

        static int send_count = 0;
        static object cs_count = new object();

        // 비동기 전송 완료시 호출되는 콜백 메소드
        public void process_send(SocketAsyncEventArgs e)
        {
            if(e.BytesTransferred <= 0 || e.SocketError != SocketError.Success)
            {
                return;
            }

            lock(this.cs_sending_queue)
            {
                // count가 0 이하일 경우는 없다...
                if(this.sending_queue.Count <= 0)
                {
                    throw new Exception("Sending queue count is less than zero!");
                }

                // 패킷 하나를 다 못 보낼 경우
                int size = this.sending_queue.Peek().position;

                if(e.BytesTransferred != size)
                {
                    string error = string.Format("Need to send more! transferred {0}, packet size {1}", e.BytesTransferred, size);
                    Console.WriteLine(error);
                    return;
                }

                // System.Threading.Interlocked.Increment(ref send_count)
                lock(cs_count)
                {
                    ++send_count;
                    {
                        Console.WriteLine(string.Format("process send : {0}, transferred {1}, send count {2}",
                            e.SocketError, e.BytesTransferred, send_count));
                    }
                }

                // 전송 완료된 패킷을 큐에서 제거한다.
                this.sending_queue.Dequeue();

                // 아직 전송되지 않는 대기중인 패킷이 있다면 다시한번 전송을 요청한다.
                if(this.sending_queue.Count > 0)
                {
                    start_send();
                }
            }
        }

        public void disconnect()
        {
            try
            {
                this.socket.Shutdown(SocketShutdown.Send);
            }

            catch(Exception)
            {

            }

            this.socket.Close();
        }

        public void start_KeepAlive()
        {
            System.Threading.Timer KeepAlive = new System.Threading.Timer((object e) =>
            {
                CPacket msg = CPacket.create(0);
                msg.push(0);
                send(msg);
            }, null, 0, 3000);
        }
    }
}
