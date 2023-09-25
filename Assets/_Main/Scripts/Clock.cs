/*
MIT License

Copyright (c) 2019 Radek Lžičař

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using UnityEngine;
using UnityEngine.Assertions;

namespace Chessticle
{
    public class Clock
    {
        public Clock(int timeSeconds)
        {
            m_WhiteTime = timeSeconds;
            m_BlackTime = timeSeconds;
        }

        public void SwitchPlayer(double serverTime)
        {
            if (!m_Running)
            {
                return;
            }

            switch (m_CurrentPlayer)
            {
                case Color.None:
                    m_CurrentPlayer = Color.Black;
                    break;
                case Color.White:
                    m_WhiteTime -= serverTime - m_LastSwitchServerTime;
                    m_CurrentPlayer = Color.Black;
                    break;
                case Color.Black:
                    m_BlackTime -= serverTime - m_LastSwitchServerTime;
                    m_CurrentPlayer = Color.White;
                    break;
            }

            if (m_WhiteTime < 0)
            {
                m_WhiteTime = 0d;
            }

            if (m_BlackTime < 0)
            {
                m_BlackTime = 0d;
            }

            m_LastSwitchServerTime = serverTime;
        }

        public float GetTime(Color color, double serverTime)
        {
            Assert.IsTrue(color != Color.None);

            float delta = 0;
            if (m_Running && m_CurrentPlayer == color)
            {
                delta = (float)(serverTime - m_LastSwitchServerTime);
            }

            var time = color == Color.White ? m_WhiteTime : m_BlackTime;
            var value = Mathf.Max(0, (float)(time - delta));
            return value;
        }

        public void Stop()
        {
            m_Running = false;
        }

        Color m_CurrentPlayer = Color.None;
        bool m_Running = true;
        double m_LastSwitchServerTime;
        double m_WhiteTime;
        double m_BlackTime;
    }
}