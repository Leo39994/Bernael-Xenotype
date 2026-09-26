using System;
using UnityEngine;

namespace Bernael.DarkMirageDemo
{
    // Shared by the game and the editor preview. Built once per captured pawn,
    // then sampled by the shader; no per-frame CPU work or extra save payload.
    public static class MirageDistanceField
    {
        public static Texture2D Create(Texture2D snapshot)
        {
            Vector4 ignored;
            return Create(snapshot,out ignored);
        }

        public static Texture2D Create(Texture2D snapshot, out Vector4 revealBounds)
        {
            int width = snapshot.width, height = snapshot.height;
            Color32[] pixels = snapshot.GetPixels32();
            int bottom=height, top=-1;
            for(int i=0;i<pixels.Length;i++)
            {
                if(pixels[i].a<8) continue;
                int row=i/width;
                bottom=Math.Min(bottom,row);
                top=Math.Max(top,row);
            }
            revealBounds = top<bottom ? new Vector4(0,1,0,0) :
                new Vector4((float)bottom/height,(float)(top+1)/height,0,0);
            float[] outside = Transform(pixels,width,height,true);
            float[] inside = Transform(pixels,width,height,false);
            float range = width * 0.125f;
            for (int i=0; i<pixels.Length; i++)
            {
                float distance = (float)(Math.Sqrt(outside[i])-Math.Sqrt(inside[i]));
                byte encoded = (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f+distance/(2*range))*255);
                pixels[i] = new Color32(encoded,encoded,encoded,255);
            }
            var result = new Texture2D(width,height,TextureFormat.RGBA32,false,true) {
                name = "DarkMirage silhouette distance", filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            result.SetPixels32(pixels);
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
