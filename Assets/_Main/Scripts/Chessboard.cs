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

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

namespace Chessticle
{
    // Uses the 0x88 board representation - https://www.chessprogramming.org/0x88
    public class Chessboard : MonoBehaviour
    {
        void Awake()
        {
            SetInitialPosition();
            CurrentPlayer = Color.White;
        }

        public (Piece, Color) GetPiece(int rank, int file)
        {
            var idx = CoordsToIndex0X88(rank, file);
            return GetPiece(idx);
        }

        public (Piece, Color) GetPiece(int idx)
        {
            var value = m_Board[idx];
            return (BoardValueToPiece(value), BoardValueToColor(value));
        }

        public bool IsVirgin(int idx)
        {
            var value = m_Board[idx];
            return (value & k_VirginMask) == k_VirginMask;
        }

        public void SetPiece(int idx, Piece piece, Color color, bool virgin)
        {
            m_Board[idx] = (byte)(PieceToBoardValue(piece, color) | (virgin ? k_VirginMask : 0));
        }

        public (int rank, int file) GetCheckCoords()
        {
            if (IsInCheck(Color.White, out var kingIdx))
            {
                return Index0X88ToCoords(kingIdx);
            }

            if (IsInCheck(Color.Black, out kingIdx))
            {
                return Index0X88ToCoords(kingIdx);
            }

            return (-1, -1);
        }

        public Color CurrentPlayer { private set; get; }
        Color NextPlayer => CurrentPlayer == Color.White ? Color.Black : Color.White;

        public bool IsLegalPromotionMove(int startIdx, int targetIdx)
        {
            var (piece, _) = GetPiece(startIdx);

            bool isOnPromotionRank = targetIdx is (>= 0 and <= 0x07) or (>= 0x70 and <= 0x77);

            bool isPromotion = IsPawn(piece) && isOnPromotionRank;

            return isPromotion && IsLegalMove(startIdx, targetIdx, out _);
        }

        static bool IsPawn(Piece piece)
        {
            return piece != Piece.None && piece <= Piece.BlackPawn;
        }

        public bool TryMove(int startIdx, int targetIdx, Piece promotionPiece, out MoveResult result)
        {
            result = MoveResult.None;
            var color = BoardValueToColor(m_Board[startIdx]);
            if (color != CurrentPlayer)
            {
                Assert.IsTrue(false);
                return false;
            }

            if (!IsLegalMove(startIdx, targetIdx, out var move))
            {
                return false;
            }

            MakeMove(move, promotionPiece);

            m_HalfMoveCountSincePawnMoveOrCapture += 1;
            bool isCapture = move.CapturedValue != k_EmptySquare;
            bool isPawnMove = IsPawn(BoardValueToPiece(move.StartValue));
            if (isCapture || isPawnMove)
            {
                m_HalfMoveCountSincePawnMoveOrCapture = 0;
            }

            m_CurrentEnPassantTargetIdx = move.EnPassantTargetIdx;

            bool nextPlayerInCheck = IsInCheck(NextPlayer, out _);
            var nextPlayerHasMoves = HasAnyLegalMoves(NextPlayer);
            bool checkmate = nextPlayerInCheck && !nextPlayerHasMoves;
            bool stalemate = !nextPlayerInCheck && !nextPlayerHasMoves;

            if (checkmate)
            {
                result = NextPlayer == Color.White ? MoveResult.WhiteCheckmated : MoveResult.BlackCheckmated;
            }
            else if (stalemate)
            {
                result = MoveResult.StaleMate;
            }


            m_LastMove = move;
            CurrentPlayer = CurrentPlayer == Color.White ? Color.Black : Color.White;
            var currentPositionHash = ZobristHashing.HashPosition(m_Board, CurrentPlayer);
            m_PositionCountsByHash.TryAdd(currentPositionHash, 0);

            m_PositionCountsByHash[currentPositionHash] += 1;

            bool threefoldRepetition = m_PositionCountsByHash[currentPositionHash] >= 3;
            bool fiftyMoveRule = m_HalfMoveCountSincePawnMoveOrCapture >= 100;
            CanDrawBeClaimed = fiftyMoveRule || threefoldRepetition;
            return true;
        }

        public bool CanDrawBeClaimed { private set; get; }

        public void UndoLastMove()
        {
            if (m_LastMove.IsEmpty) return;

            // this currently doesn't undo the en-passant target etc., but it doesn't matter because this 
            // method is only called in order to revert the move the local player tried to make
            // right before running out of time
            UnmakeMove(m_LastMove);
        }

        void ClearBoard()
        {
            m_Board[k_InvalidSquareIdx] = 0xff;

            int idx = 0;
            for (int i = 0; i < 64; i++)
            {
                m_Board[idx] = 0;
                idx = (idx + 9) & ~8; // 0x88 board index increment
            }
        }

        void SetInitialPosition()
        {
            ClearBoard();
            SetRankPieces(0, "rnbqkbnr");
            SetRankPieces(1, "pppppppp");

            SetRankPieces(6, "PPPPPPPP");
            SetRankPieces(7, "RNBQKBNR");
        }


        bool IsInCheck(Color kingColor, out int kingIdx)
        {
            kingIdx = k_InvalidSquareIdx;
            for (int i = 0; i < 64; i++)
            {
                var idx = IndexToIndex0X88(i);
                var (piece, color) = GetPiece(idx);
                if (piece == Piece.King && color == kingColor)
                {
                    kingIdx = idx;
                    break;
                }
            }

            return IsAttacked(kingIdx, kingColor);
        }

        static bool IsOffBoardIndex(int idx)
        {
            return (idx & 0x88) != 0;
        }

        bool IsAttacked(int squareIdx, Color attackedColor)
        {
            var knightOffsets = s_MovementOffsetsByPiece[Piece.Knight];

            // check if the square is attacked by a knight
            foreach (var offset in knightOffsets)
            {
                var attackerIdx = squareIdx + offset;
                if (IsOffBoardIndex(attackerIdx))
                {
                    continue;
                }

                var (attackingPiece, attackingColor) = GetPiece(attackerIdx);
                bool isAttackedByKnight = attackingPiece == Piece.Knight && attackingColor != attackedColor;

                if (isAttackedByKnight)
                {
                    return true;
                }
            }

            // check if the square is attacked by anything other than a knight
            var offsets = s_MovementOffsetsByPiece[Piece.Queen]; // all the possible directions
            foreach (var offset in offsets)
            {
                var attackerIdx = squareIdx;
                const int maxDistance = 7;
                for (int n = 0; n < maxDistance; n++)
                {
                    attackerIdx += offset;
                    if (IsOffBoardIndex(attackerIdx))
                    {
                        break;
                    }

                    var (attackingPiece, attackingColor) = GetPiece(attackerIdx);
                    if (attackingPiece == Piece.None)
                    {
                        continue;
                    }

                    if (attackingColor == attackedColor)
                    {
                        // can't be attacked by my own piece
                        break;
                    }

                    bool isOneSquareAway = n == 0;
                    bool diagonal = offset is -15 or 15 or -17 or 17;
                    bool isAttackedByBlackPawn = attackingPiece == Piece.BlackPawn
                                                 && isOneSquareAway && diagonal && offset < 0;
                    bool isAttackedByWhitePawn = attackingPiece == Piece.WhitePawn
                                                 && isOneSquareAway && diagonal && offset > 0;
                    bool isAttackedByPawn = isAttackedByBlackPawn || isAttackedByWhitePawn;

                    bool isAttackedByRook = attackingPiece == Piece.Rook && !diagonal;
                    bool isAttackedByBishop = attackingPiece == Piece.Bishop && diagonal;
                    bool isAttackedByQueen = attackingPiece == Piece.Queen;
                    bool isAttackedByKing = attackingPiece == Piece.King && isOneSquareAway;

                    if (isAttackedByPawn || isAttackedByRook || isAttackedByBishop || isAttackedByQueen
                        || isAttackedByKing)
                    {
                        return true;
                    }

                    // stop on the first non-empty square
                    break;
                }
            }

            return false;
        }

        struct Move
        {
            public int StartIdx;
            public int TargetIdx;
            public byte StartValue;
            public byte CapturedValue;
            public int CaptureIdx;
            public int EnPassantTargetIdx;

            public bool IsEmpty => StartIdx == 0 && TargetIdx == 0;
        }

        static bool IsVirgin(byte squareValue)
        {
            return (squareValue & k_VirginMask) == k_VirginMask;
        }

        public static (int rank, int file) Index0X88ToCoords(int sq0X88)
        {
            int file = sq0X88 & 7;

            int rank = sq0X88 >> 4;
            return (rank, file);
        }

        public static int CoordsToIndex0X88(int rank, int file)
        {
            var idx = rank * 8 + file;
            return IndexToIndex0X88(idx);
        }

        static int IndexToIndex0X88(int idx)
        {
            var res = idx + (idx & ~7);
            return res;
        }

        static Piece BoardValueToPiece(byte squareValue)
        {
            return (Piece)(squareValue & 7);
        }

        static Color BoardValueToColor(byte squareValue)
        {
            if (squareValue == k_EmptySquare)
            {
                return Color.None;
            }

            return (squareValue & White) == White ? Color.White : Color.Black;
        }

        static byte PieceToBoardValue(Piece piece, Color color)
        {
            return (byte)((byte)piece | (byte)color);
        }


        void MakeMove(Move move, Piece promotionPiece)
        {
            m_Board[move.StartIdx] = k_EmptySquare;

            // captureIdx can be different than targetIdx (en passant)
            m_Board[move.CaptureIdx] = k_EmptySquare;

            bool isPawnPromotion = promotionPiece != Piece.None;
            if (!isPawnPromotion)
            {
                m_Board[move.TargetIdx] = (byte)(move.StartValue & ~k_VirginMask);
            }
            else
            {
                var promotionColor = BoardValueToColor(move.StartValue);
                var promotionValue = PieceToBoardValue(promotionPiece, promotionColor);
                m_Board[move.TargetIdx] = promotionValue;
            }

            var offset = move.TargetIdx - move.StartIdx;
            var (piece, color) = GetPiece(move.TargetIdx);

            bool isCastling = piece == Piece.King &&
                              (offset == 2 || offset == -2);
            if (isCastling)
            {
                bool isKingSideCastling = offset == 2;
                var rookIdx = isKingSideCastling ? move.StartIdx + 3 : move.StartIdx - 4;
                m_Board[rookIdx] = k_EmptySquare;
                int rookIdxAfterCastling = move.TargetIdx + (isKingSideCastling ? -1 : 1);
                m_Board[rookIdxAfterCastling] = PieceToBoardValue(Piece.Rook, color);
            }
        }

        void UnmakeMove(Move move)
        {
            m_Board[move.StartIdx] = move.StartValue;
            m_Board[move.TargetIdx] = k_EmptySquare;
            m_Board[move.CaptureIdx] = move.CapturedValue;
            var offset = move.TargetIdx - move.StartIdx;
            var (piece, color) = GetPiece(move.StartIdx);

            bool isCastling = piece == Piece.King &&
                              (offset == 2 || offset == -2);
            if (isCastling)
            {
                bool isKingSideCastling = offset == 2;
                var rookIdx = isKingSideCastling ? move.StartIdx + 3 : move.StartIdx - 4;
                m_Board[rookIdx] = (byte)(PieceToBoardValue(Piece.Rook, color) | k_VirginMask);
                int rookIdxAfterCastling = move.TargetIdx + (isKingSideCastling ? -1 : +1);
                m_Board[rookIdxAfterCastling] = k_EmptySquare;
            }
        }

        bool HasAnyLegalMoves(Color color)
        {
            for (int i = 0; i < 64; i++)
            {
                var idx = IndexToIndex0X88(i);
                var value = m_Board[idx];
                if (BoardValueToColor(value) == color && GetMoves(idx).Any())
                {
                    return true;
                }
            }

            return false;
        }

        bool IsLegalMove(int startIdx, int targetIdx, out Move move)
        {
            move = GetMoves(startIdx).FirstOrDefault(i => i.TargetIdx == targetIdx);
            return !move.IsEmpty;
        }

        IEnumerable<Move> GetMoves(int startIdx)
        {
            var (piece, color) = GetPiece(startIdx);
            if (piece == Piece.None)
            {
                yield break;
            }

            bool isSlidingPiece = piece >= Piece.Bishop;
            bool isPawn = IsPawn(piece);
            bool isKing = piece == Piece.King;
            const int maxSlidingDistance = 7;
            var maxSlidingStepCount = isSlidingPiece ? maxSlidingDistance : 1;
            var offsets = s_MovementOffsetsByPiece[piece];
            int nextEnPassantTargetIdx = k_InvalidSquareIdx;
            foreach (var offset in offsets)
            {
                int targetIdx = startIdx;
                for (int n = 0; n < maxSlidingStepCount; n++)
                {
                    targetIdx += offset;
                    int enPassantCaptureIdx = k_InvalidSquareIdx;
                    if (IsOffBoardIndex(targetIdx))
                    {
                        break;
                    }

                    var targetSquare = m_Board[targetIdx];
                    bool isMyPieceOnTargetSquare =
                        targetSquare != k_EmptySquare && BoardValueToColor(targetSquare) == color;
                    if (isMyPieceOnTargetSquare)
                    {
                        break;
                    }

                    if (isKing)
                    {
                        bool isKingSideCastling = offset == 2;
                        bool isQueenSideCastling = offset == -2;
                        if (isKingSideCastling || isQueenSideCastling)
                        {
                            var skippedSquareIdx = startIdx + offset / 2;
                            bool kingPassesThroughAttackedSquare =
                                IsAttacked(startIdx, color) || IsAttacked(skippedSquareIdx, color)
                                                            || IsAttacked(targetIdx, color);
                            if (kingPassesThroughAttackedSquare)
                            {
                                break;
                            }

                            bool isVirginKing = IsVirgin(m_Board[startIdx]);
                            if (!isVirginKing)
                            {
                                break;
                            }

                            int castlingRookIdx = isKingSideCastling ? startIdx + 3 : startIdx - 4;
                            var castlingRookValue = m_Board[castlingRookIdx];
                            var min = Mathf.Min(castlingRookIdx, startIdx);
                            var max = Mathf.Max(castlingRookIdx, startIdx);
                            bool isSomethingBetweenKingAndRook = false;
                            for (int i = min + 1; i < max; i++)
                            {
                                if (m_Board[i] != k_EmptySquare)
                                {
                                    isSomethingBetweenKingAndRook = true;
                                    break;
                                }
                            }

                            if (isSomethingBetweenKingAndRook)
                            {
                                break;
                            }

                            bool isVirginRookAvailable = BoardValueToPiece(castlingRookValue) == Piece.Rook
                                                         && IsVirgin(castlingRookValue);
                            if (!isVirginRookAvailable)
                            {
                                break;
                            }
                        }
                    }
                    else if (isPawn && !IsValidPawnMove(m_Board, color, startIdx, offset,
                                 m_CurrentEnPassantTargetIdx,
                                 out nextEnPassantTargetIdx, out enPassantCaptureIdx))
                    {
                        break;
                    }

                    int captureIdx = enPassantCaptureIdx == k_InvalidSquareIdx
                        ? targetIdx
                        : enPassantCaptureIdx;
                    var captured = m_Board[captureIdx];
                    var move = new Move
                    {
                        StartValue = m_Board[startIdx],
                        StartIdx = startIdx,
                        TargetIdx = targetIdx,
                        CapturedValue = captured,
                        CaptureIdx = captureIdx,
                        EnPassantTargetIdx = nextEnPassantTargetIdx
                    };

                    MakeMove(move, Piece.None);
                    bool isLegalMove = !IsInCheck(color, out _);
                    UnmakeMove(move);

                    if (isLegalMove)
                    {
                        yield return move;
                    }

                    if (captured != k_EmptySquare)
                    {
                        break;
                    }
                }
            }
        }

        static bool IsValidPawnMove(
            byte[] board, Color color, int startIdx, int offset,
            int currentEnPassantTargetIdx, // square that is eligible for en-passant capture
            out int nextEnPassantTargetIdx, // square that will be eligible for en-passant capt. after this move
            out int enPassantCaptureIdx // square captured en-passant by this move
        )
        {
            enPassantCaptureIdx = k_InvalidSquareIdx;
            nextEnPassantTargetIdx = k_InvalidSquareIdx;

            var targetIdx = startIdx + offset;
            var targetSquare = board[targetIdx];

            bool startingOnInitialRank = color == Color.Black
                ? startIdx is >= 0x10 and <= 0x17
                : startIdx is >= 0x60 and <= 0x67;
            bool isDoubleMove = Mathf.Abs(offset) == 32;
            int skippedIdx = (startIdx + (offset / 2));

            if (isDoubleMove)
            {
                nextEnPassantTargetIdx = targetIdx;
            }

            bool diagonal = (offset & 1) != 0;
            bool isTargetEmpty = targetSquare == k_EmptySquare;
            bool isOpponentOnTargetSquare = !isTargetEmpty && BoardValueToColor(board[targetIdx]) != color;
            var skippedSquare = board[skippedIdx];
            var enPassantTargetPiece = BoardValueToPiece(board[currentEnPassantTargetIdx]);
            var squareNextToCapturingPawnIdx = targetIdx - (offset > 0 ? 16 : -16);
            var isValidEnPassantCapture =
                diagonal && (squareNextToCapturingPawnIdx == currentEnPassantTargetIdx)
                         && IsPawn(enPassantTargetPiece);
            var isValidCapture = (diagonal && isOpponentOnTargetSquare) || isValidEnPassantCapture;
            bool isValidPawnMove =
                (!isDoubleMove && !diagonal && isTargetEmpty) ||
                (isDoubleMove && startingOnInitialRank && isTargetEmpty && skippedSquare == k_EmptySquare)
                || isValidCapture;
            if (isValidEnPassantCapture)
            {
                enPassantCaptureIdx = currentEnPassantTargetIdx;
            }

            return isValidPawnMove;
        }

        void SetRankPieces(int rank, string rankPieces)
        {
            Assert.AreEqual(8, rankPieces.Length);
            Assert.IsTrue(rank is >= 0 and < 8);
            for (int file = 0; file < 8; file++)
            {
                var pieceChar = rankPieces[file];
                if (pieceChar == '.') continue;
                bool isWhite = char.IsUpper(pieceChar);
                var color = char.IsUpper(pieceChar) ? Color.White : Color.Black;
                var piece = CharToPiece(pieceChar, color);
                var idx = 16 * rank + file;
                m_Board[idx] = (byte)piece;
                m_Board[idx] |= isWhite ? White : Black;
                bool isCastlingPiece = piece == Piece.King || piece == Piece.Rook;
                if (isCastlingPiece)
                {
                    m_Board[idx] |= k_VirginMask;
                }
            }
        }

        public static Piece CharToPiece(char c, Color color)
        {
            switch (char.ToLower(c))
            {
                case 'b':
                    return Piece.Bishop;
                case 'k':
                    return Piece.King;
                case 'n':
                    return Piece.Knight;
                case 'q':
                    return Piece.Queen;
                case 'r':
                    return Piece.Rook;
                case 'p' when color == Color.White:
                    return Piece.WhitePawn;
                case 'p' when color == Color.Black:
                    return Piece.BlackPawn;
            }

            return Piece.None;
        }

        static readonly Dictionary<Piece, int[]> s_MovementOffsetsByPiece = new Dictionary<Piece, int[]>
        {
            {
                Piece.WhitePawn, new[] { -16, -32, -15, -17 }
            },
            {
                Piece.BlackPawn, new[] { 16, 32, 15, 17 }
            },
            {
                Piece.King, new[] { -1, 1, -2, 2, -15, 15, -16, -17, 16, 17 }
            },
            {
                Piece.Queen, new[] { -1, 1, -15, 15, -16, -17, 16, 17 }
            },
            {
                Piece.Bishop, new[] { -15, 15, -17, 17 }
            },
            {
                Piece.Rook, new[] { -1, 1, -16, 16 }
            },
            {
                Piece.Knight, new[] { -16 + 2, -16 - 2, 16 + 2, 16 - 2, -32 + 1, -32 - 1, 32 + 1, 32 - 1 }
            }
        };

        public static int MaxBoardValue => (byte)Piece.Queen | Black | k_VirginMask;
        const byte k_EmptySquare = 0;
        int m_HalfMoveCountSincePawnMoveOrCapture;
        readonly Dictionary<ulong, int> m_PositionCountsByHash = new Dictionary<ulong, int>();
        public const byte White = 1 << 4;
        public const byte Black = 1 << 5;
        const byte k_VirginMask = 1 << 6;
        const int k_InvalidSquareIdx = 128;
        int m_CurrentEnPassantTargetIdx = k_InvalidSquareIdx;
        Move m_LastMove;

        readonly byte[] m_Board = new byte[128 + 1]; // 0x88 board representation
    }
}