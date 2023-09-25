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
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.Assertions;

namespace Chessticle
{
    public class RoomManager : MonoBehaviourPunCallbacks
    {
        void Awake()
        {
            Assert.AreEqual(1, FindObjectsOfType<RoomManager>().Length);
        }

        public void JoinOrCreateRoom(string preferredRoomName)
        {
            StopAllCoroutines();
            const float timeoutSeconds = 15f; 
            StartCoroutine(CheckTimeoutCoroutine(timeoutSeconds));
            StartCoroutine(JoinOrCreateRoomCoroutine(preferredRoomName));
        }

        IEnumerator CheckTimeoutCoroutine(float timeout)
        {
            DidTimeout = false;
            while (!PhotonNetwork.InRoom && timeout >= 0)
            {
                yield return null;
                timeout -= Time.deltaTime;
            }

            if (timeout <= 0)
            {
                DidTimeout = true;
                StopAllCoroutines();
            }
        }

        public bool DidTimeout { private set; get; }

        static IEnumerator JoinOrCreateRoomCoroutine(string preferredRoomName)
        {
            if (PhotonNetwork.InRoom)
            {
                PhotonNetwork.LeaveRoom();
            }
            
            if (!PhotonNetwork.IsConnected)
            {
                PhotonNetwork.ConnectUsingSettings();
            }

            while (!PhotonNetwork.IsConnectedAndReady)
            {
                yield return null;
            }

            if (!PhotonNetwork.InLobby && PhotonNetwork.NetworkClientState != ClientState.JoiningLobby)
            {
                while (PhotonNetwork.NetworkClientState != ClientState.ConnectedToMasterServer)
                {
                    yield return null;
                }

                PhotonNetwork.JoinLobby();
            }

            while (PhotonNetwork.NetworkClientState != ClientState.JoinedLobby)
            {
                yield return null;
            }

            if (preferredRoomName != null)
            {
                PhotonNetwork.JoinOrCreateRoom(preferredRoomName, s_RoomOptions, TypedLobby.Default);
            }
            else
            {
                PhotonNetwork.JoinRandomRoom();
            }
        }

        static readonly RoomOptions s_RoomOptions = new RoomOptions
        {
            MaxPlayers = 2,
            // don't destroy an empty room immediately - it can be rejoined (see GameManager::Start())
            EmptyRoomTtl = 5 * 1000 
        };

        public override void OnJoinRandomFailed(short returnCode, string message)
        {
            PhotonNetwork.CreateRoom(null, s_RoomOptions, TypedLobby.Default);
        }

        public override void OnJoinRoomFailed(short returnCode, string message)
        {
            PhotonNetwork.CreateRoom(null, s_RoomOptions, TypedLobby.Default);
        }
    }
}
