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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Chessticle
{
    public class ChessboardUI : MonoBehaviour
    {
        public Sprite[] PieceSprites;
        public Chessboard Chessboard;
        public SVGImage PieceTemplate;
        public GameObject MessageTextParent;
        public RectTransform BoardRect;
        public Text TopTimeText;
        public Text BottomTimeText;
        public Button ResignButton;
        public Button NewOpponentButton;
        public Button ClaimDrawButton;
        public Image CheckIndicatorImage;
        public GameObject PromotionUI;
        public GameObject LoadingIndicator;
        public Button OfferDrawButton;
        public Button AcceptDrawButton;
        
        public Image[,] panel = new Image[8, 8];
        public Image[] panel1;
        public Image[,] cube = new Image[8, 8];
        public Image[] cube1;


        public delegate void LocalPlayerMovedCallback(int startIdx, int targetIdx, Piece promotionPiece);

        public event Action OpponentMoveFinished;
        public event Action ResignationRequested;
        public event Action NewOpponentRequested;
        public event Action ClaimDrawRequested;
        public event Action OfferDrawRequested;
        public event LocalPlayerMovedCallback LocalPlayerMoved;

        void Start()
        {
            HidePromotionUI();
            m_MessageText = MessageTextParent.GetComponentInChildren<Text>();
            m_SquareSize = BoardRect.rect.size / 8;
            var pieceSize = m_SquareSize * 0.7f;
            CheckIndicatorImage.GetComponent<RectTransform>().sizeDelta = m_SquareSize;
            PieceTemplate.GetComponent<RectTransform>().sizeDelta = pieceSize;

            foreach (var sprite in PieceSprites)
            {
                var color = sprite.name[0] == 'w' ? Color.White : Color.Black;
                var piece = Chessboard.CharToPiece(sprite.name[1], color);
                var dict = color == Color.White ? m_ImagePoolsByPieceWhite : m_ImagePoolsByPieceBlack;
                if (!dict.ContainsKey(piece))
                {
                    dict[piece] = new PieceImagePool(PieceTemplate, sprite, piece);
                }
            }

            Destroy(PieceTemplate.gameObject);
            Refresh();

            SetDraggingEnabled(Color.White, false);
            SetDraggingEnabled(Color.Black, false);
            
            //For Getting Move For The Piece
            int n = 0;
            for(int i = 0; i < 8; i++)
            {
                for(int j = 0; j < 8; j++)
                {
                    panel[i, j] = panel1[n];
                    panel[i, j].gameObject.SetActive(false);
                    panel[i,j].GetComponent<RectTransform>().sizeDelta = m_SquareSize;
                    n++;
                }
            }
            n = 0;
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    cube[i, j] = cube1[n];
                    cube[i, j].gameObject.SetActive(false);
                    cube[i,j].GetComponent<RectTransform>().sizeDelta = m_SquareSize;
                    n++;
                }
            }
        }

        (int rank, int file) PointerPositionToBoardCoords(Camera eventCamera, Vector2 eventPosition)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(BoardRect, eventPosition,
                eventCamera, out var position))
            {
                var boardSize = BoardRect.rect.size;
                var pos01 = (position + boardSize / 2) / boardSize;
                var rank = (int) Mathf.Floor(pos01.y * 8);
                var file = (int) Mathf.Floor(pos01.x * 8);
                if (m_LocalPlayerColor == Color.White)
                {
                    rank = 7 - rank;
                }
                else
                {
                    file = 7 - file;
                }

                return (rank, file);
            }

            Assert.IsTrue(false);
            return (-1, -1);
        }

        public void OnStartMove(PointerEventData data)
        {
            (m_MoveStartRank, m_MoveStartFile)
                = PointerPositionToBoardCoords(data.pressEventCamera, data.pressPosition);
            var (piece, color) = chessboard.GetPiece(m_MoveStartRank, m_MoveStartFile);
            if (color != Color.Black)
            {
                for (int i = 0; i < 8; i++)
                {
                    for (int j = 0; j < 8; j++)
                    {
                        var targetIdx = Chessboard.CoordsToIndex0X88(i, j);
                        if (chessboard.IsLegalMove(startIdx, targetIdx, out var move))
                        {
                            int i1 = 7 - i;
                            int j1 = 7 - j;
                            panel[i, j].rectTransform.anchoredPosition = GetAnchoredPosition(i1, j);
                            panel[i, j].gameObject.SetActive(true);
                        }
                    }
                }
            }
            else
            {
                for (int i = 7; i >= 0; i--)
                {
                    for (int j = 7; j >= 0; j--)
                    {
                        var targetIdx = Chessboard.CoordsToIndex0X88(i, j);
                        if (chessboard.IsLegalMove(startIdx, targetIdx, out var move))
                        {
                            int j1 = 7 - j;
                            panel[i, j].rectTransform.anchoredPosition = GetAnchoredPosition(i,j1);
                            panel[i, j].gameObject.SetActive(true);
                        }
                    }
                }
            }
                
        }

        public void OnEndMove(PointerEventData data)
        {
            var (endRank, endFile) = PointerPositionToBoardCoords(data.pressEventCamera, data.position);
            StartCoroutine(DoFinishLocalMove(m_MoveStartRank, m_MoveStartFile, endRank, endFile));
            
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    panel[i, j].gameObject.SetActive(false);
                }
            }
        }


        public void OnPromotionPieceSelected(Piece piece)
        {
            m_PromotionPiece = piece;
        }

        IEnumerator DoFinishLocalMove(int startRank, int startFile, int targetRank, int targetFile)
        {
            var startIdx = Chessboard.CoordsToIndex0X88(startRank, startFile);
            var targetIdx = Chessboard.CoordsToIndex0X88(targetRank, targetFile);
            var promotionPiece = Piece.None;
            if (Chessboard.IsLegalPromotionMove(startIdx, targetIdx))
            {
                ShowPromotionUI();
                m_PromotionPiece = Piece.None;
                while (m_PromotionPiece == Piece.None)
                {
                    yield return null;
                }

                promotionPiece = m_PromotionPiece;
                HidePromotionUI();
            }

            if (TryMove(startIdx, targetIdx, promotionPiece))
            {
                LocalPlayerMoved?.Invoke(startIdx, targetIdx, promotionPiece);
            }

            Refresh();
        }

        public void OfferDrawButton_Click()
        {
            OfferDrawRequested?.Invoke();
        }

        public void ResignButton_Click()
        {
            ResignationRequested?.Invoke();
        }

        public void NewOpponentButton_Click()
        {
            NewOpponentRequested?.Invoke();
        }

        public void ClaimDraw_Click()
        {
            ClaimDrawRequested?.Invoke();
        }

        public void ShowAcceptDrawButton()
        {
            AcceptDrawButton.gameObject.SetActive(true);
        }

        public void HideAcceptDrawButton()
        {
            AcceptDrawButton.gameObject.SetActive(false);
        }

        public void UndoLastMove()
        {
            Chessboard.UndoLastMove();
            Refresh();
        }

        public void StartGame(Color localPlayerColor)
        {
            if (localPlayerColor == Color.White)
            {
                m_WhiteTimeText = BottomTimeText;
                m_BlackTimeText = TopTimeText;
            }
            else
            {
                m_WhiteTimeText = TopTimeText;
                m_BlackTimeText = BottomTimeText;
            }

            m_LocalPlayerColor = localPlayerColor;
            SetDraggingEnabled(Color.White, localPlayerColor == Color.White);
            SetDraggingEnabled(Color.Black, false);

            Refresh();
        }

        public void StartOpponentMoveAnimation(int startIdx, int targetIdx, Piece promotionPiece)
        {
            StartCoroutine(DoAnimateOpponentMove(startIdx, targetIdx, promotionPiece));
        }

        IEnumerator DoAnimateOpponentMove(int startIdx, int targetIdx, Piece promotionPiece)
        {
            var (piece, color) = Chessboard.GetPiece(startIdx);
            bool virgin = Chessboard.IsVirgin(startIdx);
            Chessboard.SetPiece(startIdx, Piece.None, Color.None, false);
            Refresh();

            var pools = color == Color.White ? m_ImagePoolsByPieceWhite : m_ImagePoolsByPieceBlack;
            var pool = pools[piece];
            var image = pool.GetImage();
            image.transform.SetAsLastSibling();

            var (startRank, startFile) = Chessboard.Index0X88ToCoords(startIdx);
            var (targetRank, targetFile) = Chessboard.Index0X88ToCoords(targetIdx);

            Vector2 startPos;
            Vector2 targetPos;
            if (color == Color.White)
            {
                startPos = GetAnchoredPosition(startRank, 7 - startFile);
                targetPos = GetAnchoredPosition(targetRank, 7 - targetFile);
            }
            else
            {
                startPos = GetAnchoredPosition(7 - startRank, startFile);
                targetPos = GetAnchoredPosition(7 - targetRank, targetFile);
            }

            float t = 0;
            while (t < 1f)
            {
                t += Time.deltaTime * 5f;
                var pos = Vector2.Lerp(startPos, targetPos, t);
                image.rectTransform.anchoredPosition = pos;
                yield return null;
            }

            image.rectTransform.anchoredPosition = targetPos;

            Chessboard.SetPiece(startIdx, piece, color, virgin);
            bool didMove = TryMove(startIdx, targetIdx, promotionPiece);
            Assert.IsTrue(didMove);
            Refresh();
            OpponentMoveFinished?.Invoke();
        }

        public MoveResult LastMoveResult { private set; get; }

        public void SetResignButtonActive(bool active)
        {
            ResignButton.gameObject.SetActive(active);
        }

        public void SetNewOpponentButtonActive(bool active)
        {
            NewOpponentButton.gameObject.SetActive(active);
        }

        public void ShowMessage(string message)
        {
            m_MessageText.text = message;
            MessageTextParent.SetActive(true);
        }

        public void HideMessage()
        {
            MessageTextParent.SetActive(false);
        }

        public void ShowLoadingIndicator()
        {
            LoadingIndicator.SetActive(true);
        }

        public void HideLoadingIndicator()
        {
            LoadingIndicator.SetActive(false);
        }

        public void ShowTime(Color color, float timeSeconds)
        {
            var span = TimeSpan.FromSeconds(timeSeconds);
            var format = timeSeconds < 60 ? @"mm\:ss\.f" : @"mm\:ss";
            var time = span.ToString(format);
            if (color == Color.White)
            {
                m_WhiteTimeText.text = time;
            }
            else
            {
                m_BlackTimeText.text = time;
            }
        }

        public void ShowCurrentPlayerIndicator(Color color)
        {
            m_WhiteTimeText.GetComponentInChildren<Image>(true).enabled = color == Color.White;
            m_BlackTimeText.GetComponentInChildren<Image>(true).enabled = color == Color.Black;
        }

        bool TryMove(int startIdx, int targetIdx, Piece promotionPiece)
        {
            bool didMove = Chessboard.TryMove(startIdx, targetIdx, promotionPiece, out var result);
            if (didMove)
            {
                SetDraggingEnabled(m_LocalPlayerColor,
                    Chessboard.CurrentPlayer == m_LocalPlayerColor);
                LastMoveResult = result;
            }

            return didMove;
        }

        public Color CurrentPlayer => Chessboard.CurrentPlayer;

        void Refresh()
        {
            HideAll();

            CheckIndicatorImage.gameObject.SetActive(false);
            var (checkRank, checkFile) = Chessboard.GetCheckCoords();
            if (m_LocalPlayerColor == Color.White)
            {
                checkRank = 7 - checkRank;
            }
            else
            {
                checkFile = 7 - checkFile;
            }

            for (int rank = 0; rank < 8; rank++)
            {
                for (int file = 0; file < 8; file++)
                {
                    if (rank == checkRank && file == checkFile)
                    {
                        CheckIndicatorImage.rectTransform.anchoredPosition = GetAnchoredPosition(rank, file);
                        CheckIndicatorImage.gameObject.SetActive(true);
                    }

                    int r = m_LocalPlayerColor == Color.White ? 7 - rank : rank;
                    int f = m_LocalPlayerColor == Color.White ? file : 7 - file;

                    var (piece, color) = Chessboard.GetPiece(r, f);
                    if (piece == Piece.None)
                    {
                        continue;
                    }

                    var pool = color == Color.White
                        ? m_ImagePoolsByPieceWhite[piece]
                        : m_ImagePoolsByPieceBlack[piece];

                    var image = pool.GetImage();
                    image.rectTransform.anchoredPosition = GetAnchoredPosition(rank, file);
                }
            }
        }

        Vector2 GetAnchoredPosition(int rank, int file)
        {
            return new Vector2((file + 0.5f) * m_SquareSize.x, (rank + 0.5f) * m_SquareSize.y);
        }

        void HideAll()
        {
            foreach (var piece in m_ImagePoolsByPieceWhite.Keys)
            {
                m_ImagePoolsByPieceWhite[piece].HideAll();
            }

            foreach (var piece in m_ImagePoolsByPieceBlack.Keys)
            {
                m_ImagePoolsByPieceBlack[piece].HideAll();
            }
        }

        public void RefreshClaimDrawButton()
        {
            ClaimDrawButton.gameObject.SetActive(Chessboard.CanDrawBeClaimed);
        }

        void HideClaimDrawButton()
        {
            ClaimDrawButton.gameObject.SetActive(false);
        }

        public void ShowOfferDrawButton()
        {
            OfferDrawButton.gameObject.SetActive(true);
        }

        public void HideOfferDrawButton()
        {
            OfferDrawButton.gameObject.SetActive(false);
        }

        public void StopGame()
        {
            StopAllCoroutines();
            SetDraggingEnabled(Color.White, false);
            SetDraggingEnabled(Color.Black, false);
            HidePromotionUI();
            SetResignButtonActive(false);
            SetNewOpponentButtonActive(true);
            RefreshClaimDrawButton();
            HideLoadingIndicator();
            HideClaimDrawButton();
            HideAcceptDrawButton();
            HideOfferDrawButton();
        }

        void HidePromotionUI()
        {
            PromotionUI.SetActive(false);
        }

        void ShowPromotionUI()
        {
            PromotionUI.transform.SetAsLastSibling(); // move to front
            PromotionUI.SetActive(true);
        }

        void SetDraggingEnabled(Color color, bool draggingEnabled)
        {
            var pools = color == Color.White ? m_ImagePoolsByPieceWhite : m_ImagePoolsByPieceBlack;
            foreach (var key in pools.Keys)
            {
                var pool = pools[key];
                pool.SetDraggingEnabled(draggingEnabled);
            }
        }

        Piece m_PromotionPiece;
        Text m_WhiteTimeText;
        Text m_BlackTimeText;
        Text m_MessageText;
        int m_MoveStartRank;
        int m_MoveStartFile;
        Color m_LocalPlayerColor;
        Vector2 m_SquareSize;
        readonly Dictionary<Piece, PieceImagePool> m_ImagePoolsByPieceWhite = new Dictionary<Piece, PieceImagePool>();
        readonly Dictionary<Piece, PieceImagePool> m_ImagePoolsByPieceBlack = new Dictionary<Piece, PieceImagePool>();
    }
}
