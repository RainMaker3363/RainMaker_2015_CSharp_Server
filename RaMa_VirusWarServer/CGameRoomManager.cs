using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaMa_VirusWarServer
{
    // 게임 방을 관리하는 매니저
    class CGameRoomManager
    {
        List<CGameRoom> rooms;

        public CGameRoomManager()
        {
            this.rooms = new List<CGameRoom>();
        }

        // 매칭을 요청한 유저들을 넘겨 받아 게임 방을 생성한다.
        public void create_room(CGameUser user1, CGameUser user2)
        {
            // 게임방을 생성하여 입장 시킴
            CGameRoom battleroom = new CGameRoom();
            battleroom.enter_gameroom(user1, user2);

            // 방 리스트에 추가 하여 관리한다.
            this.rooms.Add(battleroom);
        }

        public void remove_room(CGameRoom room)
        {
            room.destroy();
            this.rooms.Remove(room);
        }
    }
}
