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

using System.Collections;
using System.Globalization;
using Photon.Pun;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

namespace Chessticle
{
    public class GameManager : MonoBehaviourPun
    {
        void Awake()
        {
            Assert.AreEqual(1, FindObjectsOfType<GameManager>().Length);

            m_RoomManager = GetComponent<RoomManager>();
            m_ChessboardUI = FindObjectOfType<ChessboardUI>();
            m_ChessboardUI.LocalPlayerMoved += OnLocalPlayerMoved;
            m_ChessboardUI.OpponentMoveFinished += OnMoveFinished;
            m_ChessboardUI.ResignationRequested += Resign;
            m_ChessboardUI.NewOpponentRequested += FindNewOpponent;
            m_ChessboardUI.ClaimDrawRequested += ClaimDraw;
            m_ChessboardUI.OfferDrawRequested += OfferDraw;
            m_ChessboardUI.RefreshClaimDrawButton();
        }

        void OnDestroy()
        {
            m_ChessboardUI.ResignationRequested -= Resign;
            m_ChessboardUI.LocalPlayerMoved -= OnLocalPlayerMoved;
            m_ChessboardUI.OpponentMoveFinished -= OnMoveFinished;
            m_ChessboardUI.NewOpponentRequested -= FindNewOpponent;
            m_ChessboardUI.ClaimDrawRequested -= ClaimDraw;
            m_ChessboardUI.OfferDrawRequested -= OfferDraw;
        }

        void OnLocalPlayerMoved(int startIdx, int targetIdx, Piece promotionPiece)
        {
            photonView.RPC(nameof(RpcMove), RpcTarget.AllViaServer,
                (byte) startIdx, (byte) targetIdx, promotionPiece);
        }

        void Resign()
        {
            photonView.RPC(nameof(RpcResign), RpcTarget.AllViaServer, m_LocalPlayerColor);
        }

        void ClaimDraw()
        {
            photonView.RPC(nameof(RpcClaimDraw), RpcTarget.AllViaServer);
        }

        void OfferDraw()
        {
            if (m_PendingDrawOfferByOpponent)
            {
                // the opponent offered a draw to us, so just claim it instead of offering a
                // draw back to them
                ClaimDraw();
            }
            else
            {
                m_ChessboardUI.HideOfferDrawButton();
                photonView.RPC(nameof(RpcOfferDraw), RpcTarget.Others);
            }
        }

        static void FindNewOpponent()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        void StartGame()
        {
            Assert.IsTrue(PhotonNetwork.IsMasterClient);
            PhotonNetwork.CurrentRoom.IsOpen = false;
            var randomMasterClientColor = Random.Range(0, 1f) < 0.5f ? Color.White : Color.Black;
            photonView.RPC(nameof(RpcStartGame), RpcTarget.AllViaServer, randomMasterClientColor);
        }

        static Color OpponentColor(Color color)
        {
            return color == Color.White ? Color.Black : Color.White;
        }

        [PunRPC]
        void RpcClaimDraw()
        {
            m_DrawClaimed = true;
        }

        [PunRPC]
        void RpcStartGame(Color masterClientColor, PhotonMessageInfo info)
        {
            m_LocalPlayerColor =
                PhotonNetwork.IsMasterClient ? masterClientColor : OpponentColor(masterClientColor);

            m_OpponentColor = OpponentColor(m_LocalPlayerColor);

            m_StartTime = info.SentServerTime;
            m_ChessboardUI.StartGame(m_LocalPlayerColor);
            m_GameStarted = true;
            
            // This field has the same value on both clients.
            // It's used to speed up the matchmaking process when there are only a few players
            // online - when starting a new game (after a checkmate, for instance), both the
            // players will first try to create/join a room with this name so they are more likely
            // to quickly end up in a room with each other again.
            // (If this wasn't used, the players would often end up in two separate rooms
            // waiting for each other)
            s_PreferredRoomName = $"r{m_StartTime.ToString(CultureInfo.InvariantCulture)}";
        }

        [PunRPC]
        void RpcMove(byte startIdx, byte targetIdx, Piece promotionPiece, PhotonMessageInfo info)
        {
            bool isMyMove = info.Sender.ActorNumber == PhotonNetwork.LocalPlayer.ActorNumber;
            var currentPlayer = isMyMove ? m_LocalPlayerColor : m_OpponentColor;
            var nextPlayer = OpponentColor(currentPlayer);

            bool timeout = m_Clock.GetTime(currentPlayer, info.SentServerTime) <= 0;

            if (!timeout)
            {
                m_Clock.SwitchPlayer(info.SentServerTime);
                m_ChessboardUI.ShowCurrentPlayerIndicator(nextPlayer);
                m_AtLeastOneMoveWasMade = true;
                if (isMyMove)
                {
                    OnMoveFinished();
                }
                else
                {
                    m_ChessboardUI.StartOpponentMoveAnimation(startIdx, targetIdx, promotionPiece);
                }
            }

            if (timeout && isMyMove)
            {
                m_ChessboardUI.UndoLastMove();
            }
        }

        void OnMoveFinished()
        {
            m_ChessboardUI.HideAcceptDrawButton();
            m_ChessboardUI.ShowOfferDrawButton();
            m_PendingDrawOfferByOpponent = false;

            m_ChessboardUI.RefreshClaimDrawButton();
            m_LastConfirmedMoveResult = m_ChessboardUI.LastMoveResult;
        }

        [PunRPC]
        void RpcTimeout(Color color)
        {
            if (m_OutOfTimePlayer == Color.None)
            {
                m_OutOfTimePlayer = color;
            }
        }

        [PunRPC]
        void RpcResign(Color color)
        {
            if (m_ResigningPlayer == Color.None)
            {
                m_ResigningPlayer = color;
            }
        }

        [PunRPC]
        void RpcOfferDraw()
        {
            m_PendingDrawOfferByOpponent = true;
            m_ChessboardUI.ShowAcceptDrawButton();
        }

        void StopGame()
        {
            m_Clock.Stop();
            m_ChessboardUI.StopGame();
        }

        void ShowGameResult(Color winningPlayer, string whiteWinText, string blackWinText)
        {
            switch (winningPlayer)
            {
                case Color.White:
                    ShowMessage(whiteWinText);
                    break;
                case Color.Black:
                    ShowMessage(blackWinText);
                    break;
                case Color.None:
                    ShowMessage("Draw.");
                    break;
            }
        }

        IEnumerator Start()
        {
            var wait = new WaitForSeconds(0.1f);
            bool timeOutRpcCalled = false;
            double nextTimeToCheckOtherRooms = 0;
            int retryCount = 0;

            while (true)
            {
                bool stateJustEntered = m_State != m_PreviousState;
                m_PreviousState = m_State;

                switch (m_State)
                {
                    case State.Connecting:
                        if (stateJustEntered)
                        {
                            ShowMessage("Connecting...");
                            m_RoomManager.JoinOrCreateRoom(s_PreferredRoomName);
                            m_ChessboardUI.SetResignButtonActive(false);
                            m_ChessboardUI.SetNewOpponentButtonActive(false);
                            m_ChessboardUI.ShowLoadingIndicator();
                            m_ChessboardUI.HideOfferDrawButton();
                            m_ChessboardUI.HideAcceptDrawButton();
                        }

                        bool timeRunning = PhotonNetwork.Time > 0;
                        bool isInRoomAndReady = PhotonNetwork.InRoom && timeRunning;
                        if (isInRoomAndReady)
                        {
                            m_State = State.WaitingForOpponent;
                        }
                        else if (m_RoomManager.DidTimeout)
                        {
                            m_State = State.ConnectionError;
                        }

                        break;

                    case State.WaitingForOpponent:
                        const float checkOtherRoomInterval = 10f;
                        if (stateJustEntered)
                        {
                            ShowMessage("Waiting for an opponent...");
                            nextTimeToCheckOtherRooms = PhotonNetwork.Time + checkOtherRoomInterval;
                        }

                        if (m_GameStarted)
                        {
                            m_State = State.WaitingForFirstMove;
                        }
                        else if (PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom?.PlayerCount == 2)
                        {
                            StartGame();
                        }
                        else if (PhotonNetwork.Time > nextTimeToCheckOtherRooms && retryCount < 2)
                        {
                            retryCount += 1;
                            // After a few seconds, try and check if there's another room with
                            // someone else waiting for an opponent.
                            // This needs to be done in order to avoid situations where two players are waiting
                            // for each other forever in their own rooms because they both created
                            // a room at the same time. 

                            // This will leave the current room and either join a room created by 
                            // someone else, or rejoin this room if no other rooms are available
                            m_RoomManager.JoinOrCreateRoom(null);
                            nextTimeToCheckOtherRooms = PhotonNetwork.Time + checkOtherRoomInterval;
                        }
                        else if (m_RoomManager.DidTimeout)
                        {
                            m_State = State.ConnectionError;
                        }
                        else if (!PhotonNetwork.IsConnected)
                        {
                            m_State = State.ConnectionError;
                        }

                        break;

                    case State.WaitingForFirstMove:
                        if (stateJustEntered)
                        {
                            m_ChessboardUI.HideLoadingIndicator();
                            m_ChessboardUI.ShowTime(Color.White, k_StartClockTime);
                            m_ChessboardUI.ShowTime(Color.Black, k_StartClockTime);
                            m_ChessboardUI.ShowCurrentPlayerIndicator(Color.White);
                        }

                        const float limit = 30f;
                        var abortTime = m_StartTime + limit;
                        var secondsLeft = Mathf.Ceil((float) (abortTime - PhotonNetwork.Time));

                        var msg = m_LocalPlayerColor == Color.Black ? "White has" : "You have";

                        ShowMessage($"{msg} {secondsLeft} seconds to play the first move");

                        if (secondsLeft <= 0 || PhotonNetwork.CurrentRoom?.PlayerCount < 2)
                        {
                            m_State = State.Aborted;
                        }
                        else if (m_AtLeastOneMoveWasMade)
                        {
                            m_State = State.Playing;
                        }
                        else if (!PhotonNetwork.IsConnected)
                        {
                            m_State = State.ConnectionError;
                        }

                        break;
                    case State.Playing:
                        if (stateJustEntered)
                        {
                            m_ChessboardUI.SetResignButtonActive(true);
                            HideMessage();
                            m_ChessboardUI.ShowOfferDrawButton();
                        }

                        var currentPlayer = m_ChessboardUI.CurrentPlayer;

                        var localPlayerTime = m_Clock.GetTime(m_LocalPlayerColor, PhotonNetwork.Time);
                        bool timeout = localPlayerTime <= 0;
                        if (timeout && !timeOutRpcCalled)
                        {
                            timeOutRpcCalled = true;
                            photonView.RPC(nameof(RpcTimeout), RpcTarget.AllViaServer, m_LocalPlayerColor);
                        }

                        var currentPlayerTime = m_Clock.GetTime(currentPlayer, PhotonNetwork.Time);
                        m_ChessboardUI.ShowTime(currentPlayer, currentPlayerTime);

                        if (m_LastConfirmedMoveResult != MoveResult.None)
                        {
                            switch (m_LastConfirmedMoveResult)
                            {
                                case MoveResult.WhiteCheckmated:
                                    ShowMessage("Black won by checkmate.");
                                    break;
                                case MoveResult.BlackCheckmated:
                                    ShowMessage("White won by checkmate.");
                                    break;
                                case MoveResult.StaleMate:
                                    ShowMessage("Stalemate.");
                                    break;
                            }

                            m_State = State.EndOfGame;
                        }
                        else if (m_OutOfTimePlayer != Color.None)
                        {
                            ShowGameResult(OpponentColor(m_OutOfTimePlayer),
                                "White won on time.", "Black won on time.");
                            m_State = State.EndOfGame;
                        }
                        else if (m_ResigningPlayer != Color.None)
                        {
                            ShowGameResult(OpponentColor(m_ResigningPlayer),
                                "White won by resignation.", "Black won by resignation.");
                            m_State = State.EndOfGame;
                        }
                        else if (PhotonNetwork.CurrentRoom?.PlayerCount == 1)
                        {
                            ShowGameResult(m_LocalPlayerColor,
                                "White won. Black left the game.",
                                "Black won. White left the game.");
                            m_State = State.EndOfGame;
                        }
                        else if (!PhotonNetwork.IsConnectedAndReady)
                        {
                            m_State = State.ConnectionError;
                        }
                        else if (m_DrawClaimed)
                        {
                            ShowGameResult(Color.None, null, null);
                            m_State = State.EndOfGame;
                        }

                        break;
                    case State.Aborted:
                        if (stateJustEntered)
                        {
                            ShowMessage("The game was aborted.");
                            StopGame();
                        }

                        break;

                    case State.EndOfGame:
                        if (stateJustEntered)
                        {
                            StopGame();
                        }

                        break;
                    case State.ConnectionError:
                        if (stateJustEntered)
                        {
                            ShowMessage("Connection error. Try again.");
                            StopGame();
                        }

                        break;
                }

                yield return wait;
            }
            // ReSharper disable once IteratorNeverReturns
        }

        void ShowMessage(string text)
        {
            m_ChessboardUI.ShowMessage(text);
        }

        void HideMessage()
        {
            m_ChessboardUI.HideMessage();
        }

        enum State
        {
            Connecting,
            WaitingForOpponent,
            WaitingForFirstMove,
            Playing,
            EndOfGame,
            Aborted,
            ConnectionError,
        }

        static string s_PreferredRoomName;
        Color m_LocalPlayerColor;
        Color m_OpponentColor;
        Color m_OutOfTimePlayer = Color.None;
        Color m_ResigningPlayer = Color.None;
        MoveResult m_LastConfirmedMoveResult;
        bool m_DrawClaimed;

        const int k_StartClockTime = 10 * 60;
        readonly Clock m_Clock = new(k_StartClockTime);
        bool m_GameStarted;
        bool m_AtLeastOneMoveWasMade;
        double m_StartTime;
        State? m_PreviousState;
        State m_State;
        bool m_PendingDrawOfferByOpponent;
        ChessboardUI m_ChessboardUI;
        RoomManager m_RoomManager;
    }
}