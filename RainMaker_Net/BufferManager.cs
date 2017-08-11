using System;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RainMaker_Net
{
    // 이 클래스는 single Large 버퍼를 만들어 SocketAsyncEveArgs 객체의 Socket I/O 바이트 전송을
    // 저장해놓는 클래스이다.
    internal class BufferManager
    {
        // bytes 버퍼풀의 숫자
        int m_numBytes;
        // 버퍼 매니저에 할당되는 바이트 배열들
        byte[] m_buffer;
        Stack<int> m_freeIndexPool;
        int m_currentIndex;
        int m_bufferSize;

        public BufferManager(int totalBytes, int bufferSize)
        {
            m_numBytes = totalBytes;
            m_currentIndex = 0;
            m_bufferSize = bufferSize;
            m_freeIndexPool = new Stack<int>();
        }

        // 버퍼 풀의 공간을 할당해준다.
        public void InitBuffer()
        {
            // SocketAsyncEventArg 객체에게 나눠줄 바이트를 생성한다.
            m_buffer = new byte[m_numBytes];
        }

        // SocketAsyncEventArgs 객체에 버퍼를 좀더 추가(확장)시캬준다.
        public bool SetBuffer(SocketAsyncEventArgs args)
        {
            if(m_freeIndexPool.Count > 0)
            {
                args.SetBuffer(m_buffer, m_freeIndexPool.Pop(), m_bufferSize);
            }
            else
            {
                if((m_numBytes - m_bufferSize) < m_currentIndex)
                {
                    return false;
                }

                args.SetBuffer(m_buffer, m_currentIndex, m_bufferSize);
                m_currentIndex += m_bufferSize;
            }

            return true;
        }

        // SocketAsyncEvetArg 객체의 버퍼를 지워준다.
        public void FreeBuffer(SocketAsyncEventArgs args)
        {
            m_freeIndexPool.Push(args.Offset);
            args.SetBuffer(null, 0, 0);
        }
    }
}
