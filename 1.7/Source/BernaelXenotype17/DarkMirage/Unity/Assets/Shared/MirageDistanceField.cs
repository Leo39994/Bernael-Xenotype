using System;
using UnityEngine;

namespace Bernael_Xenotype
{
    // Shared by the game and the editor preview. Built once per captured pawn,
    // then sampled by the shader; no per-frame CPU work or extra save payload.
    public static class MirageDistanceField
    {
        public sealed class Data
        {
            public readonly int Width, Height;
            public readonly Color32[] Pixels;
            public readonly Vector4 RevealBounds;
            // Field/mesh rectangle in the original capture's UV coordinates.
            public readonly Vector4 CanvasRect;

            public Data(int width, int height, Color32[] pixels, Vector4 revealBounds, Vector4 canvasRect)
            {
                Width = width;
                Height = height;
                Pixels = pixels;
                RevealBounds = revealBounds;
                CanvasRect = canvasRect;
            }
        }

        public static Texture2D Create(Texture2D snapshot)
        {
            Vector4 ignored;
            return Create(snapshot,out ignored);
        }

        public static Texture2D Create(Texture2D snapshot, out Vector4 revealBounds)
        {
            Data data = Compute(snapshot.GetPixels32(), snapshot.width, snapshot.height);
            revealBounds = data.RevealBounds;
            return CreateTexture(data);
        }

        // Owns this pixel array. Pure array/math work; safe on a worker thread.
        // Texture readback and upload remain on Unity's main thread.
        public static Data Compute(Color32[] pixels, int width, int height)
        {
            int bottom=height, top=-1, left=width, right=-1;
            var columnTop = new int[width];
            var rowLeft = new int[height];
            var rowRight = new int[height];
            for (int y=0;y<height;y++) rowLeft[y]=width;
            for(int i=0;i<pixels.Length;i++)
            {
                if(pixels[i].a<8) continue;
                int row=i/width, column=i%width;
                bottom=Math.Min(bottom,row);
                top=Math.Max(top,row);
                left=Math.Min(left,column);
                right=Math.Max(right,column);
                if(pixels[i].a<128) continue;
                columnTop[column]=Math.Max(columnTop[column],row+1);
                rowLeft[row]=Math.Min(rowLeft[row],column);
                rowRight[row]=Math.Max(rowRight[row],column+1);
            }
            Vector4 revealBounds = top<bottom ? new Vector4(0,1,0,1) :
                new Vector4((float)bottom/height,(float)(top+1)/height,(float)left/width,(float)(right+1)/width);
            // Reserve space for the shader's tongues, smooth joins, stroke and
            // glow. Keep the original pixel density and capture UVs: saved
            // snapshots, eye anchors and world-space flame sizes stay valid.
            int padLeft = Math.Max(0, (int)Math.Ceiling(width * 0.25f) - left);
            int padRight = Math.Max(0, right + 1 + (int)Math.Ceiling(width * 0.25f) - width);
            int padBottom = Math.Max(0, (int)Math.Ceiling(height * 0.125f) - bottom);
            int padTop = Math.Max(0, top + 1 + (int)Math.Ceiling(height * 0.375f) - height);
            int fieldWidth = width + padLeft + padRight;
            int fieldHeight = height + padBottom + padTop;
            var field = new Color32[fieldWidth * fieldHeight];
            for (int y = 0; y < height; y++)
                Array.Copy(pixels, y * width, field, (y + padBottom) * fieldWidth + padLeft, width);
            var canvasRect = new Vector4(-(float)padLeft / width, -(float)padBottom / height,
                (float)fieldWidth / width, (float)fieldHeight / height);
            float[] outside = Transform(field,fieldWidth,fieldHeight,true);
            float[] inside = Transform(field,fieldWidth,fieldHeight,false);
            float range = width * 0.125f;
            for (int i=0; i<field.Length; i++)
            {
                float distance = (float)(Math.Sqrt(outside[i])-Math.Sqrt(inside[i]));
                byte encoded = (byte)Math.Round(Math.Max(0f, Math.Min(1f, 0.5f+distance/(2*range)))*255);
                int x=i%fieldWidth-padLeft, y=i/fieldWidth-padBottom;
                // R: signed distance. G/B/A: top/left/right contour anchors.
                // Tongues grow from the captured outline, including hats and gear.
                field[i] = new Color32(encoded,
                    x<0 || x>=width ? (byte)0 : (byte)Math.Round(255f*columnTop[x]/height),
                    y<0 || y>=height ? (byte)255 : (byte)Math.Round(255f*rowLeft[y]/width),
                    y<0 || y>=height ? (byte)0 : (byte)Math.Round(255f*rowRight[y]/width));
            }
            return new Data(fieldWidth, fieldHeight, field, revealBounds, canvasRect);
        }

        public static Texture2D CreateTexture(Data data)
        {
            var result = new Texture2D(data.Width,data.Height,TextureFormat.RGBA32,false,true) {
                name = "DarkMirage silhouette distance", filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            result.SetPixels32(data.Pixels);
            result.Apply(false,true);
            return result;
        }

        // Exact squared Euclidean distance transform, linear in pixel count.
        private static float[] Transform(Color32[] pixels,int width,int height,bool solid)
        {
            float[] data = new float[pixels.Length];
            for (int i=0;i<data.Length;i++) data[i] = (pixels[i].a >= 128) == solid ? 0f : 1e9f;
            int size = Math.Max(width,height);
            var source = new float[size];
            var target = new float[size];
            var sites = new int[size];
            var cuts = new float[size+1];
            for(int y=0;y<height;y++)
            {
                Array.Copy(data,y*width,source,0,width);
                Line(source,target,width,sites,cuts);
                Array.Copy(target,0,data,y*width,width);
            }
            for(int x=0;x<width;x++)
            {
                for(int y=0;y<height;y++) source[y] = data[y*width+x];
                Line(source,target,height,sites,cuts);
                for(int y=0;y<height;y++) data[y*width+x] = target[y];
            }
            return data;
        }

        private static void Line(float[] source,float[] target,int count,int[] sites,float[] cuts)
        {
            int k=0;
            sites[0]=0; cuts[0]=float.NegativeInfinity; cuts[1]=float.PositiveInfinity;
            for(int q=1;q<count;q++)
            {
                float crossing;
                do
                {
                    int p=sites[k];
                    // Double arithmetic preserves q*q even when the row has no seeds.
                    crossing=(float)(((double)source[q]+q*q-source[p]-p*p)/(2*(q-p)));
                    if(crossing>cuts[k]) break;
                    k--;
                } while(k>=0);
                k++; sites[k]=q; cuts[k]=crossing; cuts[k+1]=float.PositiveInfinity;
            }
            k=0;
            for(int q=0;q<count;q++)
            {
                while(cuts[k+1]<q) k++;
                int d=q-sites[k]; target[q]=d*d+source[sites[k]];
            }
        }
    }
}
