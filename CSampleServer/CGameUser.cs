using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RainMaker_Net;

namespace CSampleServer
{
    using GameServer;

    // 하나의 Session 객체를 나타낸다.
    class CGameUser : IPeer
    {
        CUserToken token;

        public CGameUser(CUserToken token)
        {
            // 네트워크 모듈에서 클라이언트의 접속 요청, 종료 등의 처리 시 해당 인터페이스를 통해서
            // CGameUser의 메소드를 호출해주기 위함이다.
            // 네트워크 모듈에서 이런저런 처리를 한 뒤 어플리케이션으로 그 사실을 통보할때 필요하다.
            this.token = token;
            this.token.set_peer(this);
        }

        // 클라이언트로 부터 메시지가 수신되었을때 호출됩니다.
        void IPeer.on_message(Const<byte[]> buffer)
        {
            CPacket msg = new CPacket(buffer.Value, this);
            PROTOCOL protocol = (PROTOCOL)msg.pop_protocol_id();

            Console.WriteLine("------------------------------------------------------");
            Console.WriteLine("protocol id " + protocol);

            switch(protocol)
            {
                // 에코 서버는 pop_string()으로 부터 꺼내온 데이터를 그대로 push하여 응답해줍니다.
                case PROTOCOL.CHAT_MSG_REQ:
                    {
                        string text = msg.pop_string();
                        Console.WriteLine(string.Format("text {0}", text));

                        CPacket response = CPacket.create((short)PROTOCOL.CHAT_MSG_ACK);
                        response.push(text);
                        send(response);
                    }
                    break;
            }
        }

        void IPeer.on_removed()
        {
            Console.WriteLine("The client disconnected");

            Program.remove_user(this);
        }

        public void send(CPacket msg)
        {
            this.token.send(msg);
        }

        void IPeer.disconnect()
        {
            this.token.socket.Disconnect(false);
        }

        void IPeer.process_user_operation(CPacket msg)
        {

        }
    }
}
