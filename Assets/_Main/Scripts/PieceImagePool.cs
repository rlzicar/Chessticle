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

using Unity.VectorGraphics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Chessticle
{
    // A pool of SVG images for a piece
    public class PieceImagePool
    {
        public PieceImagePool(SVGImage template, Sprite sprite, Piece piece)
        {
            int maxCount = 0;
            // 8 pawns can be promoted to a rook, queen, bishop or knight
            // so there can be up to (2 + 8) rooks, (1 + 8) queens etc.
            switch (piece)
            {
                case Piece.WhitePawn:
                case Piece.BlackPawn:
                    maxCount = 8;
                    break;
                case Piece.Knight:
                    maxCount = 10;
                    break;
                case Piece.King:
                    maxCount = 1;
                    break;
                case Piece.Bishop:
                    maxCount = 10;
                    break;
                case Piece.Rook:
                    maxCount = 10;
                    break;
                case Piece.Queen:
                    maxCount = 9;
                    break;
            } 
        
            m_SvgImages = new SVGImage[maxCount];
        
            for (int i = 0; i < maxCount; i++)
            {
                var go = Object.Instantiate(template, template.transform.parent);
                var pieceImage = go.GetComponent<SVGImage>();
                pieceImage.sprite = sprite;
                pieceImage.name = sprite.name;
                m_SvgImages[i] = pieceImage;
            }
        }

        public SVGImage GetImage()
        {
            m_Idx = (m_Idx + 1) % m_SvgImages.Length;
            m_SvgImages[m_Idx].enabled = true;
            return m_SvgImages[m_Idx];
        }

        public void HideAll()
        {
            foreach (var image in m_SvgImages)
            {
                image.enabled = false;
            }
        }

        public void SetDraggingEnabled(bool enabled)
        {
            foreach (var image in m_SvgImages)
            {
                image.raycastTarget = enabled;
            }
        }

        readonly SVGImage[] m_SvgImages;
        int m_Idx;
    }
}