using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RainMaker_Net;

namespace RaMa_VirusWarServer
{

    
    // 게임 방 하나를 구성한다. 게임의 로직이 처리되는 핵심 클래스이다.
    class CGameRoom
    {
        enum PLAYER_STATE : byte
        {
            // 방에 막 입장한 상태,
            ENTERED_ROOM,

            // 로딩을 완료한 상태
            LOADING_COMPLETE,

            // 턴 진행 준비 상태
            READY_TO_TURN,

            // 턴 연출을 모두 완료한 상태
            CLEINT_TURN_FINISHED

        }

        // 게임을 진행하는 플레이어. 1P, 2P가 존재합니다.
        List<CPlayer> players;

        // 플레이어들의 상태를 관리하는 변수
        Dictionary<byte, PLAYER_STATE> player_state;

        // 현재 턴을 진행하고 있는 플레이어의 인덱스
        byte current_turn_player;

        // 게임 보드 판
        List<short> gameboard;

        // 0~49까지의 인덱스를 갖고 있는 보드판 데이터
        List<short> table_board;

        static byte COLUMN_COUNT = 7;

        readonly short EMPTY_SLOT = short.MaxValue;


        public CGameRoom()
        {
            this.players = new List<CPlayer>();
            this.player_state = new Dictionary<byte, PLAYER_STATE>();
            this.current_turn_player = 0;

            // 7*7(총 49칸)모양의 보드판을 구성한다.
            // 초기에는 모두 빈공간이므로 EMPTY_SLOT으로 채운다
            this.gameboard = new List<short>();
            this.table_board = new List<short>();

            for (byte i = 0; i < COLUMN_COUNT * COLUMN_COUNT; ++i)
            {
                this.gameboard.Add(EMPTY_SLOT);
                this.table_board.Add(i);
            }
        }

        // 매칭이 성사된 플레이어들이 게임에 입장합니다.
        public void enter_gameroom(CGameUser user1, CGameUser user2)
        {
            // 플레이어들을 생성하고 각각 1번, 2번 인덱스를 부여해준다.
            CPlayer player1 = new CPlayer(user1, 0);
            CPlayer player2 = new CPlayer(user2, 1);

            this.players.Clear();
            this.players.Add(player1);
            this.players.Add(player2);

            // 플레이어들의 초기 상태를 지정해 준다.
            this.player_state.Clear();
            change_playerstate(player1, PLAYER_STATE.ENTERED_ROOM);
            change_playerstate(player2, PLAYER_STATE.ENTERED_ROOM);

            // 로딩 시작 메시지 전송
            // 로딩 시작메세지 전송.
            this.players.ForEach(player =>
            {
                CPacket msg = CPacket.create((Int16)PROTOCOL.START_LOADING);
                msg.push(player.player_index);  // 본인의 플레이어 인덱스를 알려준다.
                player.send(msg);
            });

            user1.enter_room(player1, this);
            user2.enter_room(player2, this);
        }

        // 클라이언트에서 로딩을 완료한 후 요청함
        // 이 요청이 들어오면 게임을 시작해도 좋다는 뜻이다.
        // sender == 요청한 유저
        public void loading_complete(CPlayer sender)
        {
            // 해당 플레이어를 로딩완료 상태로 변경한다.
            change_playerstate(sender, PLAYER_STATE.LOADING_COMPLETE);

            // 모든 유저가 준비 상태인지 체크한다.
            if (!allplayers_ready(PLAYER_STATE.LOADING_COMPLETE))
            {
                // 아직 준비가 안된 유저가 있다면 대기한다.
                return;
            }

            // 모든 유저가 준비 상태인지 체크한다.
            // 아직 준비가 안된 유저가 있다면 대기한다.
            // 모두 준비 되었다면 게임을 시작한다.
            battle_start();
        }

        // 게임을 시작한다.
        void battle_start()
        {
            // 게임을 새로 시작할 때마다 초기화해줘야 할 것들..
            reset_gamedata();

            // 게임 시작 메시지 전송
            CPacket msg = CPacket.create((short)PROTOCOL.GAME_START);

            // 플레이어들의 세균 위치 전송
            msg.push((byte)this.players.Count);

            this.players.ForEach(player =>
            {
                // 누구인지 구분하기 위한 플레이어 인덱스
                msg.push(player.player_index);

                // 플레이어가 소지한 세균들의 전체 개수
                byte cell_count = (byte)player.viruses.Count;

                msg.push(cell_count);

                // 플레이어의 세균들의 위치 정보
                player.viruses.ForEach(position => msg.push(position));
            });

            // 첫 턴을 진행할 플레이어 인덱스
            msg.push(this.current_turn_player);
            broadcast(msg);
        }

        // 모든 유저들에게 메시지를 전송합니다.
        void broadcast(CPacket msg)
        {
            this.players.ForEach(player => player.send_for_broadcast(msg));
            CPacket.destroy(msg);
        }

        // 게임 데이터를 초기화 한다.
        // 게임을 새로 시작할 때 마다 초기화 해줘야 할 것들을 넣는다
        void reset_gamedata()
        {
            // 보드판 데이터 초기화
            for(int i = 0; i< this.gameboard.Count; ++i)
            {
                this.gameboard[i] = EMPTY_SLOT;
            }

            // 1번 플레이어의 세균은 왼쪽위(0,0), 오른쪽 위(0,7) 두군데에 배치합니다.
            put_virus(0, 0, 0);
            put_virus(0, 0, 6);

            // 2번 플레이어의 세균은 왼쪽 아래(7, 0) 오른쪽 아래(7,7) 두군데에 배치한다.
            put_virus(1, 6, 0);
            put_virus(1, 6, 6);


            // 턴 초기화.
            this.current_turn_player = 0;   // 1P부터 시작.
        }

        // 모든 플레이어가 특정 상태가 되었는지를 판단한다.
        // 모든 플레이어가 같은 상태에 있다면 true, 한명이라도 다른 상태에 있다면 false를 리턴한다.
        bool allplayers_ready(PLAYER_STATE state)
        {
            foreach (KeyValuePair<byte, PLAYER_STATE> kvp in this.player_state)
            {
                if (kvp.Value != state)
                {
                    return false;
                }
            }

            return true;
        }

        // 플레이어의 상태를 변경합니다.
        void change_playerstate(CPlayer player, PLAYER_STATE state)
        {
            if(this.player_state.ContainsKey(player.player_index))
            {
                this.player_state[player.player_index] = state;
            }
            else
            {
                this.player_state.Add(player.player_index, state);
            }
        }

        // 모든 플레이어가 특정 상태가 되었는지를 판단한다.
        // 모든 플레이어가 같은 상태에 있다면 true, 한명이라도 다른 상태에 있다면 false를 리턴한다.
        bool allPlayer_ready(PLAYER_STATE state)
        {
            foreach(KeyValuePair<byte, PLAYER_STATE> kvp in this.player_state)
            {
                if(kvp.Value != state)
                {
                    return false;
                }
            }

            return true;
        }

        // 클라이언트의 이동 요청
        // sender = 요청한 유저
        // begin_pos = 시작 위치
        // target_pos = 이동하고자 하는 위치
        public void moving_req(CPlayer sender, short begin_pos, short target_pos)
        {
            // sender 차례인지 체크
            // 체크 이유 : 현재 자신의 차례가 아님에도 불구하고 이동 요청을 보내온다면 게임의 턴이 엉망이 됩니다.
            if(this.current_turn_player != sender.player_index)
            {
                // 현재 턴이 아닌 플레이어가 보낸 요청이라면 무시한다.
                // 이런 비정상적인 상황에서는 화면이나 파일로 로그를 남겨두는 것이 좋다
                return;
            }



            // begin_pos에 sender의 캐릭터가 존재하는지 체크
            // 체크 이유 : 없는 캐릭터를 이동하려고 하면 안됩니다.
            // Begin_pos에 Sender의 세균이 존재하는지 체크
            if (this.gameboard[begin_pos] != sender.player_index)
            {
                // 시작 위치에 해당 플레이어의 세균이 존재하지 않는다.
                return;
            }


            // target_pos가 이동 또는 복제 가능한 범위인지 체크
            // 체크 이유 : 이동할 수 없는 범위로는 갈 수 없도록 처리해야 합니다.
            if (this.gameboard[target_pos] != EMPTY_SLOT)
            {
                // 목적지는 0으로 설정된 빈 곳이어야 한다.
                // 다른 세균이 자리하고 있는 곳으로 이동할 수 없다.
                return;
            }

            short distance = CHelper.get_distance(begin_pos, target_pos);

            if(distance > 2)
            {
                // 2칸을 초과하는 거리는 이동할 수 없다.
                return;
            }

            
            if(distance <= 0)
            {
                // 자기 자신의 위치로는 이동할 수 없다.
                return;
            }

            // 모든 체크가 정상이라면 이동을 처리합니다.

            if(distance == 1)
            {
                // 이동 거리가 한 칸일 경우에는 복제를 수행한다.

                // 이전 위치에 있는 세균을 삭제한다.
                put_virus(sender.player_index, target_pos);
            }
            else if(distance == 2)
            {
                // 이동 거리가 두 칸일 경우에는 이동을 수행한다.
                remove_virus(sender.player_index, begin_pos);

                // 새로운 위치에 세균을 놓는다.
                put_virus(sender.player_index, target_pos);
            }

            // 세균을 이동하여 로직 처리를 수행합니다.
            // 전염시킬 상대방 세균이 있다면 룰에 맞게 전염시킵니다.

            // 목적지를 기준으로 주위에 존재하는 상대방 세균을 감염시켜 같은 편으로 만듭니다.
            CPlayer opponent = get_opponent_player();
            infect(target_pos, sender, opponent);


            // 최종 결과를 모든 클라이언트들에게 전송합니다.
            CPacket msg = CPacket.create((short)PROTOCOL.PLAYER_MOVED);

            msg.push(sender.player_index);  // 누가
            msg.push(begin_pos);            // 어디서
            msg.push(target_pos);           // 어디로 이동 했는지

            broadcast(msg);

            // 턴을 종료합니다
            //turn_end();
        }


        // 플레이어 인덱스에 해당하는 플레이어를 구한다.
        CPlayer get_player(byte player_index)
        {
            return this.players.Find(obj => obj.player_index == player_index);
        }

        // 현재 턴인 플레이어의 상대 플레이어를 구한다.
        CPlayer get_opponent_player()
        {
            return this.players.Find(player => player.player_index != this.current_turn_player);
        }

        // 현재 턴을 진행중인 플레이어를 구한다.
        CPlayer get_current_player()
        {
            return this.players.Find(player => player.player_index == this.current_turn_player);
        }

        // 보드판에 플레이어의 세균을 배치한다.
        void put_virus(byte player_index, byte row, byte col)
        {
            short position = CHelper.get_position(row, col);
            put_virus(player_index, position);
        }

        // 보드판에 플레이어의 세균을 배치합니다.
        void put_virus(byte player_index, short position)
        {
            this.gameboard[position] = player_index;

            // 추후에 어느 플레이어가 몇마리의 세균을 보유했는지등을 계산할때
            // 쉽게 하기 위해서 플레이어의 객체에도 세균 정보를 추가합니다.
            get_player(player_index).add_cell(position);
        }

        // 배치된 세균을 삭제한다.
        void remove_virus(byte player_index, short position)
        {
            this.gameboard[position] = EMPTY_SLOT;
            get_player(player_index).remove_cell(position);
        }

        // 상대방의 세균을 감염시킨다.
        public void infect(short basis_cell, CPlayer attacker, CPlayer victim)
        {
            // 방어자의 세균 중에 기존위치로 부터 1칸 반경에 있는 세균들이 감염 대상입니다.
            List<short> neighbors = CHelper.find_neighbor_cells(basis_cell, victim.viruses, 1);

            foreach(short position in neighbors)
            {
                // 방어자의 세균을 삭제한다.
                remove_virus(victim.player_index, position);

                // 공격자의 세균을 추가한다.
                put_virus(attacker.player_index, position);
            }
        }



        // 클라이언트에서 턴 연출이 모두 완료 되었을 때 호출합니다.
        public void turn_finished(CPlayer sender)
        {
            change_playerstate(sender, PLAYER_STATE.CLEINT_TURN_FINISHED);

            if(!allPlayer_ready(PLAYER_STATE.CLEINT_TURN_FINISHED))
            {
                return;
            }

            // 턴을 넘긴다.
            turn_end();
        }

        // 턴을 종료합니다. 게임으 끝났는지 확인하는 과정을 수행합니다.
        void turn_end()
        {
            // 보드판 상태를 확인하여 게임이 끝났는지 검사한다.
            if(!CHelper.can_play_more(this.table_board, get_current_player(), this.players))
            {
                // game over
                game_over();
                return;
            }

            // 아직 게임이 끝나지 않았다면 다음 플레이어로 턴을 넘긴다.
            if(this.current_turn_player <this.players.Count -1)
            {
                ++this.current_turn_player;
            }
            else
            {
                // 다시 첫번째 플레이어의 턴으로 만들어 준다
                this.current_turn_player = this.players[0].player_index;
            }

            start_turn();
        }

        // 턴을 시작하라고 클라이언트들에게 알려 준다.
        void start_turn()
        {
            // 턴을 진행할 수 있도록 준비 상태로 만든다.
            this.players.ForEach(player => change_playerstate(player, PLAYER_STATE.READY_TO_TURN));

            CPacket msg = CPacket.create((short)PROTOCOL.START_PLAYER_TURN);
            msg.push(this.current_turn_player);
            broadcast(msg);
        }

        // 게임 오버 처리
        void game_over()
        {
            // 승리자 가리기
            byte win_player_index = byte.MaxValue;

            int count_1p = this.players[0].get_virus_count();
            int count_2p = this.players[1].get_virus_count();

            if(count_1p == count_2p)
            {
                // 동점인 경우
                win_player_index = byte.MaxValue;
            }
            else
            {
                if(count_1p > count_2p)
                {
                    win_player_index = this.players[0].player_index;
                }
                else
                {
                    win_player_index = this.players[1].player_index;
                }
            }

            CPacket msg = CPacket.create((short)PROTOCOL.GAME_OVER);
            msg.push(win_player_index);
            msg.push(count_1p);
            msg.push(count_2p);
            broadcast(msg);

            // 방 제거
            Program.game_main.room_manager.remove_room(this);
        }

        public void destroy()
        {
            CPacket msg = CPacket.create((short)PROTOCOL.ROOM_REMOVED);
            broadcast(msg);

            this.players.Clear();
        }
    }


}
