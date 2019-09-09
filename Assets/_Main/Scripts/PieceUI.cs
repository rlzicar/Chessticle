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
using UnityEngine.EventSystems;

namespace Chessticle
{
    public class PieceUI : MonoBehaviour, IBeginDragHandler, IEndDragHandler, IDragHandler
    {
        void Awake()
        {
            if (s_ChessboardUI == null)
            {
                s_ChessboardUI = GetComponentInParent<ChessboardUI>();
            }

            m_RectTransform = GetComponent<RectTransform>();
        }

        void IBeginDragHandler.OnBeginDrag(PointerEventData eventData)
        {
            transform.SetAsLastSibling(); // move to front
            s_ChessboardUI.OnStartMove(eventData);
        }

        void IEndDragHandler.OnEndDrag(PointerEventData eventData)
        {
            s_ChessboardUI.OnEndMove(eventData);
        }

        void IDragHandler.OnDrag(PointerEventData eventData)
        {
            m_RectTransform.position = eventData.position;
        }

        RectTransform m_RectTransform;
        static ChessboardUI s_ChessboardUI;
    }
}