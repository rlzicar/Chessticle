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

using Random = System.Random;

namespace Chessticle
{
    // Used to hash board positions in order to detect position repetitions
    public static class ZobristHashing
    {
        static ZobristHashing()
        {
            const int seed = 18761234; // any seed would do
            var random = new Random(seed);

            const int squareCount = 64;
            // one random 64bit hash for each combination of a board value (piece, color, virginity)
            // and a position on the board (0 .. 63)
            s_Hashes = new ulong[Chessboard.MaxBoardValue + 1, squareCount];

            for (int i = 0; i < Chessboard.MaxBoardValue; i++)
            {
                for (int j = 0; j < squareCount; j++)
                {
                    s_Hashes[i, j] = (uint)random.Next() | ((ulong)random.Next() << 32);
                }
            }

            s_WhitePlayersHash = (uint)random.Next() | ((ulong)random.Next() << 32);
        }

        public static ulong HashPosition(byte[] squares0X88, Color currentPlayer)
        {
            ulong result = 0;

            // combine the hashes of all the squares using bitwise XOR
            const int squareCount = 64;
            for (int squareIdx = 0; squareIdx < squareCount; squareIdx++)
            {
                var idx0X88 = squareIdx + (squareIdx & ~7);
                int boardValue = squares0X88[idx0X88];
                bool isEmptySquare = boardValue == 0;
                if (isEmptySquare) continue;

                result ^= s_Hashes[boardValue, squareIdx];
            }

            if (currentPlayer == Color.White)
            {
                result ^= s_WhitePlayersHash;
            }

            return result;
        }

        static readonly ulong s_WhitePlayersHash;
        static readonly ulong[,] s_Hashes;
    }
}