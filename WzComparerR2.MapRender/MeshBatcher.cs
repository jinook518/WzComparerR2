﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WzComparerR2.Animation;
using WzComparerR2.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;

namespace WzComparerR2.MapRender
{
    sealed class MeshBatcher : IDisposable
    {
        public MeshBatcher(GraphicsDevice graphicsDevice)
        {
            this.GraphicsDevice = graphicsDevice;
            this.alphaBlendState = StateEx.NonPremultipled_Hidef();
        }

        public GraphicsDevice GraphicsDevice { get; private set; }
        public bool D2DEnabled { get; set; }

        //内部batcher
        SpriteBatchEx sprite;
        SkeletonMeshRenderer spineRender;
        D2DRenderer d2dRender;
        BlendState alphaBlendState;
        ItemType lastItem;

        //start参数
        private Matrix? matrix;
        private bool isInBeginEndPair;

        public void Begin(Matrix? matrix = null)
        {
            this.matrix = matrix;
            this.lastItem = ItemType.Unknown;
            this.isInBeginEndPair = true;
        }

        public void Draw(MeshItem mesh)
        {
            if (mesh.RenderObject is Frame)
            {
                Prepare(ItemType.Sprite);
                this.DrawItem(mesh, (Frame)mesh.RenderObject);
            }
            else if (mesh.RenderObject is Skeleton)
            {
                Prepare(ItemType.Skeleton);
                this.DrawItem(mesh, (Skeleton)mesh.RenderObject);
            }
            else if (mesh.RenderObject is TextMesh)
            {
                //在draw内部prepare
                var textmesh = (TextMesh)mesh.RenderObject;
                this.DrawItem(mesh, textmesh);
            }
            else if (mesh.RenderObject is LineListMesh)
            {
                if (this.D2DEnabled)
                {
                    Prepare(ItemType.D2DObject);
                }
                else
                {
                    Prepare(ItemType.Sprite);
                }
                this.DrawItem((LineListMesh)mesh.RenderObject);
            }
        }

        private void DrawItem(MeshItem mesh, Frame frame)
        {
            var origin = mesh.FlipX ? new Vector2(frame.Rectangle.Width - frame.Origin.X, frame.Origin.Y) : frame.Origin.ToVector2();
            var eff = mesh.FlipX ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            //兼容平铺
            if (mesh.TileRegion != null)
            {
                var region = mesh.TileRegion.Value;
                for (int y = region.Top; y < region.Bottom; y++)
                {
                    for (int x = region.Left; x < region.Right; x++)
                    {
                        Vector2 pos = mesh.Position + mesh.TileOffset * new Vector2(x, y);
                        sprite.Draw(frame.Texture, pos,
                            sourceRectangle: frame.AtlasRect,
                            color: new Color(Color.White, frame.A0),
                            origin: origin,
                            effects: eff
                            );
                    }
                }
            }
            else
            {
                sprite.Draw(frame.Texture, mesh.Position,
                    sourceRectangle: frame.AtlasRect,
                    color: new Color(Color.White, frame.A0),
                    origin: origin,
                    effects: eff
                    );
            }
        }

        private void DrawItem(MeshItem mesh, Skeleton skeleton)
        {
            skeleton.FlipX = mesh.FlipX;

            //兼容平铺
            if (mesh.TileRegion != null)
            {
                var region = mesh.TileRegion.Value;
                for (int y = region.Top; y < region.Bottom; y++)
                {
                    for (int x = region.Left; x < region.Right; x++)
                    {
                        Vector2 pos = mesh.Position + mesh.TileOffset * new Vector2(x, y);
                        skeleton.X = pos.X;
                        skeleton.Y = pos.Y;
                        skeleton.UpdateWorldTransform();
                        this.spineRender.Draw(skeleton);
                    }
                }
            }
            else
            {
                skeleton.X = mesh.Position.X;
                skeleton.Y = mesh.Position.Y;
                skeleton.UpdateWorldTransform();
                this.spineRender.Draw(skeleton);
            }
        }

        private void DrawItem(MeshItem mesh, TextMesh text)
        {
            if (text.Text != null && text.Font != null)
            {
                var size = text.Font.MeasureString(text.Text);
                var pos = mesh.Position;

                switch (text.Align)
                {
                    case Alignment.Near: break;
                    case Alignment.Center: pos.X -= (int)(size.X / 2); break;
                    case Alignment.Far: pos.X -= size.X; break;
                }

                object baseFont = text.Font.BaseFont ?? text.Font;

                if (baseFont is XnaFont)
                {
                    Prepare(ItemType.Sprite);
                }
                else if (baseFont is D2DFont)
                {
                    Prepare(ItemType.D2DObject);
                }
                else
                {
                    return;
                }

                if (text.BackColor.A > 0) //绘制背景
                {
                    var padding = text.Padding;
                    var rect = new Rectangle((int)(pos.X - padding.Left),
                        (int)(pos.Y - padding.Top),
                        (int)(size.X + padding.Left + padding.Right),
                        (int)(size.Y + padding.Top + padding.Bottom)
                        );
                    switch (this.lastItem)
                    {
                        case ItemType.Sprite: sprite.FillRoundedRectangle(rect, text.BackColor); break;
                        case ItemType.D2DObject: d2dRender.FillRoundedRectangle(rect, 3, text.BackColor); break;
                    }
                }

                if (text.ForeColor.A > 0) //绘制文字
                {
                    switch (this.lastItem)
                    {
                        case ItemType.Sprite: sprite.DrawStringEx((XnaFont)baseFont, text.Text, pos, text.ForeColor); break;
                        case ItemType.D2DObject: d2dRender.DrawString((D2DFont)baseFont, text.Text, pos, text.ForeColor); break;
                    }
                }
            }
        }

        private void DrawItem(LineListMesh lineList)
        {
            if (lineList != null && lineList.Lines != null)
            {
                var vertices = lineList.Lines;
                int vertexCount = vertices.Length / 2 * 2;
                if (this.D2DEnabled)
                {
                    for (int i = 0; i < vertexCount; i += 2)
                    {
                        this.d2dRender.DrawLine(vertices[i].ToVector2(), vertices[i + 1].ToVector2(),
                            lineList.Thickness, lineList.Color);
                    }
                }
                else
                {
                    for (int i = 0; i < vertexCount; i += 2)
                    {
                        sprite.DrawLine(vertices[i], vertices[i + 1], lineList.Thickness, lineList.Color);
                    }
                }
            }
        }

        public Rectangle[] Measure(MeshItem mesh)
        {
            Rectangle rect = Rectangle.Empty;

            if (mesh.RenderObject is TextMesh)
            {
                var textItem = (TextMesh)mesh.RenderObject;
                var size = textItem.Font.MeasureString(textItem.Text);
                var pos = mesh.Position;

                switch (textItem.Align)
                {
                    case Alignment.Near: break;
                    case Alignment.Center: pos.X -= size.X / 2; break;
                    case Alignment.Far: pos.X -= size.X; break;
                }

                var padding = textItem.Padding;
                var rectBg = new Rectangle((int)(pos.X - padding.Left),
                    (int)(pos.Y - padding.Top),
                    (int)(size.X + padding.Left + padding.Right),
                    (int)(size.Y + padding.Top + padding.Bottom)
                    );
                var rectText = new Rectangle((int)pos.X, (int)pos.Y, (int)size.X, (int)size.Y);
                return new Rectangle[] { Rectangle.Union(rectBg, rectText) };
            }

            if (mesh.RenderObject is Frame)
            {
                var frame = (Frame)mesh.RenderObject;
                rect = frame.Rectangle;
                if (mesh.FlipX)
                {
                    rect.X = -rect.Right;
                }
            }
            else
            {
                return new Rectangle[0];
            }

            rect.X += (int)mesh.Position.X;
            rect.Y += (int)mesh.Position.Y;

            if (mesh.TileRegion != null)
            {
                var region = mesh.TileRegion.Value;
                Rectangle[] rects = new Rectangle[region.Width * region.Height];
                Point offset = mesh.TileOffset.ToPoint();
                int i = 0;

                for (int y = region.Top; y < region.Bottom; y++)
                {
                    for (int x = region.Left; x < region.Right; x++)
                    {
                        rects[i++] = new Rectangle(rect.X + x * offset.X,
                            rect.Y + y * offset.Y,
                            rect.Width,
                            rect.Height);
                    }
                }

                return rects;
            }
            else
            {
                return new[] { rect };
            }
        }

        public void End()
        {
            InnerFlush();
            this.lastItem = ItemType.Unknown;
            this.isInBeginEndPair = false;
        }

        private void Prepare(ItemType itemType)
        {
            if (lastItem == itemType)
            {
                return;
            }

            switch (itemType)
            {
                case ItemType.Sprite:
                case ItemType.Skeleton:
                case ItemType.D2DObject:
                    InnerFlush();
                    lastItem = itemType;
                    InnerBegin();
                    break;
            }
        }

        private void InnerBegin()
        {
            switch (lastItem)
            {
                case ItemType.Sprite:
                    if (this.sprite == null)
                    {
                        this.sprite = new SpriteBatchEx(this.GraphicsDevice);
                    }
                    this.sprite.Begin(SpriteSortMode.Deferred, this.alphaBlendState, transformMatrix: this.matrix);
                    break;

                case ItemType.Skeleton:
                    if (this.spineRender == null)
                    {
                        this.spineRender = new SkeletonMeshRenderer(this.GraphicsDevice);
                    }
                    this.spineRender.Effect.World = matrix ?? Matrix.Identity;
                    this.spineRender.Begin();
                    break;

                case ItemType.D2DObject:
                    if (this.d2dRender == null)
                    {
                        this.d2dRender = new D2DRenderer(this.GraphicsDevice);
                    }
                    if (this.matrix == null)
                    {
                        this.d2dRender.Begin();
                    }
                    else
                    {
                        this.d2dRender.Begin(this.matrix.Value);
                    }
                    break;
            }
        }

        private void InnerFlush()
        {
            switch (lastItem)
            {
                case ItemType.Sprite:
                    this.sprite.End();
                    break;

                case ItemType.Skeleton:
                    this.spineRender.End();
                    break;

                case ItemType.D2DObject:
                    this.d2dRender.End();
                    break;
            }
        }

        public void Dispose()
        {
            this.sprite?.Dispose();
            this.spineRender?.Effect.Dispose();
            this.alphaBlendState.Dispose();
        }

        private enum ItemType
        {
            Unknown = 0,
            Sprite,
            Skeleton,
            D2DObject
        }

    }
}
