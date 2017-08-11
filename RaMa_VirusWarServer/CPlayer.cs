using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RainMaker_Net;

namespace RaMa_VirusWarServer
{
    // 플레이어를 구성하는 클래스입니다.
    // 실제 클라이언트는 CGameUser를 통해서 매칭되지만
    // 게임 플레이를 위해서는 CPlayer 클래스를 별도로 만들어 둡니다.
    // 왜냐하면 CGameUser에는 유저의 계정 정보, 소켓 핸들 정보가 들어 있는데
    // 이런 정보들은 플레이할때 필요 없으므로 숨겨놓고 접근하지 못하게 하는것이
    // 바람직합니다.

    // [CGameRoom ---- CPlayer ---- CGameUser]
    // CGameRoom은 CGameUser의 존재를 알지 못합니다.오직 CPlayer에만 접근할 수 있습니다.
    class CPlayer
    {
        CGameUser owner;
        public byte player_index { get; private set; }
        public List<short> viruses { get; private set; }

        public CPlayer(CGameUser user, byte player_index)
        {
            this.owner = user;
            this.player_index = player_index;
            this.viruses = new List<short>();
        }

        public void send(CPacket msg)
        {
            this.owner.send(msg);
        }

        public void reset()
        {
            this.viruses.Clear();
        }

        public void add_cell(short position)
        {
            this.viruses.Add(position);
        }

        public void remove_cell(short position)
        {
            this.viruses.Remove(position);
        }

        public void send_for_broadcast(CPacket msg)
        {
            this.owner.send(msg);
        }

        public int get_virus_count()
        {
            return this.viruses.Count;
        }
    }
}
