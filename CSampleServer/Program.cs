using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RainMaker_Net;

namespace CSampleServer
{
    class Program
    {
        // 실제로 통신을 할 유저들의 목록
        static List<CGameUser> userlist;

        static void Main(string[] args)
        {
            // 패킷을 미리 생성해놓는다.
            // 동시체 처리할 수 있는 패킷 클래스의 인스턴스가 최대 2000개 까지 가능하다는 것이다.
            // 사용이 끝난 패킷은 초기화 후 재사용 되니 문제 없다.
            CPacketBufferManager.initialize(2000);
            userlist = new List<CGameUser>();

            CNetworkService service = new CNetworkService();

            // 콜백 메소드 설정
            service.sessing_created_callback += on_session_created;

            // 초기화
            service.Initialize();

            // 어떠한 IP라도 상관없이 모두 받아 들일 수 있다.
            // 맨 마지막인 backlog값은 accept 처리 도중 대기시킬 연결 개수를 의미한다.
            service.listen("0.0.0.0", 7979, 100);

            Console.WriteLine("Welcome To RamaNet!");

            while (true)
            {
                System.Threading.Thread.Sleep(10000);
            }

            Console.ReadKey();
        }

        // 클라이언트가 접속 완료 하였을 때 호출됩니다.
        // n개의 워커 스레드에서 호출될 수 있으므로 공유 자원 접근시 동기화 처리를 해줘야 합니다.
        static void on_session_created(CUserToken token)
        {
            CGameUser user = new CGameUser(token);

            lock(userlist)
            {
                userlist.Add(user);
            }
        }

        public static void remove_user(CGameUser user)
        {
            lock(userlist)
            {
                userlist.Add(user);
            }
        }
    }
}
