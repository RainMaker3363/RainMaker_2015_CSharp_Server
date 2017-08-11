using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace RainMaker_Net
{
    // SocketAsyncEventArgs 객체들을 저장해놓는 역할입니다.
    class SocketAsyncEventArgsPool
    {
        Stack<SocketAsyncEventArgs> m_Pool;

        // 오브젝트 풀을 적정 사이즈에 맞게 초기화 합니다.
        public SocketAsyncEventArgsPool(int capacity)
        {
            m_Pool = new Stack<SocketAsyncEventArgs>(capacity);
        }

        // SocketAsyncEventArg 인스턴스 풀에 추가한다.
        public void Push(SocketAsyncEventArgs item)
        {
            if(item == null)
            {
                throw new ArgumentNullException("items Added to a SocketAsyncEventArgsPool Cannot be Null");
            }

            lock (m_Pool)
            {
                m_Pool.Push(item);
            }
        }

        // SocketAsyncEvetArgs 인스턴스 풀에 요소를 꺼낸다.
        public SocketAsyncEventArgs Pop()
        {
            lock(m_Pool)
            {
                return m_Pool.Pop();
            }
        }

        // SocketAsyncEventArgs 인스턴스 풀에 요소의 수
        public int count
        {
            get
            {
                return m_Pool.Count;
            }
        }
    }
}
