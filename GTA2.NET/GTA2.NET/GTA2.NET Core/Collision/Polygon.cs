﻿// GTA2.NET
// 
// File: Polygon.cs
// Created: 29.06.2013
// 
// 
// Copyright (C) 2010-2013 Hiale
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software
// and associated documentation files (the "Software"), to deal in the Software without restriction,
// including without limitation the rights to use, copy, modify, merge, publish, distribute,
// sublicense, and/or sell copies of the Software, and to permit persons to whom the Software
// is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies
// or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR
// IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// 
// Grand Theft Auto (GTA) is a registred trademark of Rockstar Games.
using System;
using System.Collections.Generic;
using System.Linq;
using FarseerPhysics.Common;
using FarseerPhysics.Common.Decomposition;
using Hiale.GTA2NET.Core.Helper;
using Hiale.GTA2NET.Core.Map;
using Microsoft.Xna.Framework;

namespace Hiale.GTA2NET.Core.Collision
{
    public class Polygon : VerticesEx
    {
        public void AddToObstacles(Map.Map map, int layer, List<IObstacle> obstacles)
        {
            //debug
            //if (layer >= 4 && Contains(new Vector2(69, 194)))
            //    Console.WriteLine();
            var convexPolygons = BayazitDecomposer.ConvexPartition(this);
            var blocks = GetAssociatedBlocks(convexPolygons, map, layer);
            var fill = CheckLid(blocks, map, layer, obstacles);
            if (fill)
            {
                foreach (var convexPolygon in convexPolygons)
                    AddPolygonObstacle(convexPolygon, false, obstacles, layer);
            }
            else
            {
                foreach (var convexPolygon in convexPolygons)
                    CreateLineSegments(convexPolygon, obstacles, layer);
            }
        }

        private bool CheckLid(IEnumerable<Block> blocks, Map.Map map, int layer, List<IObstacle> obstacles)
        {
            var openBlocks = 0;
            foreach (var block in blocks)
            {
                if (block.Lid)
                    continue;
                var blocksAbove = CheckBlocksAbove(block, map, layer, obstacles);
                if (!blocksAbove)
                    openBlocks++;
            }
            return openBlocks <= 0;
        }

        private static IEnumerable<Block> GetAssociatedBlocks(List<Vertices> convexPolygons, Map.Map map, int layer)
        {
            var blocks = new List<Block>();
            foreach (var convexPolygon in convexPolygons)
            {
                float minX;
                float maxX;
                float minY;
                float maxY;
                CalculateBounds(convexPolygon, out minX, out maxX, out minY, out maxY);
                maxX = (float)Math.Ceiling(maxX);
                maxY = (float)Math.Ceiling(maxY);

                var pointsCache = new Dictionary<Vector2, bool>();
                for (var y = (int)minY; y < maxY; y++)
                {
                    for (var x = (int)minX; x < maxX; x++)
                    {
                        var block = map.CityBlocks[x, y, layer];
                        var blockPoints = GetBlockPoints(block, layer);

                        var addBlock = false;
                        foreach (var blockPoint in blockPoints)
                        {
                            bool isOnPolygon;
                            if (pointsCache.TryGetValue(blockPoint, out isOnPolygon))
                                continue;
                            isOnPolygon = VerticesEx.IsPointInPolygonOrEdge(convexPolygon, blockPoint);
                            pointsCache.Add(blockPoint, isOnPolygon);
                            if (!isOnPolygon)
                                continue;
                            addBlock = true;
                            break;
                        }
                        if (addBlock && !blocks.Contains(block))
                            blocks.Add(block);
                    }
                }
            }
            Debug.SavePolygonWithBlocksPicture(convexPolygons, blocks);
            return blocks;
        }

        private bool CheckBlocksAbove(Block block, Map.Map map, int layer, List<IObstacle> obstacles)
        {
            var blockPoints = GetBlockPoints(block, layer); //ToDo: dictionary
            for (var z = (int)block.Position.Z + 1; z < 8; z++)
            {
                try
                {
                    if (map.CityBlocks[(int)block.Position.X, (int)block.Position.Y, z].Lid)
                        return true;
                }
                catch (Exception e)
                {
                    
                    System.Diagnostics.Debug.WriteLine(e);
                }
                

                var blockFilled = false;

                //ToDo: optimize!
                var layerObstacles = obstacles.Where(obstacle => obstacle.Z == z && (obstacle is PolygonObstacle || obstacle is RectangleObstacle)).ToList();

                foreach (var layerObstacle in layerObstacles)
                {
                    var containAll = true;
                    foreach (var blockPoint in blockPoints)
                    {
                        if (!layerObstacle.Bounds.Contains(blockPoint))
                        {
                            containAll = false;
                            break;
                        }
                        //
                    }
                    if (containAll)
                    {
                        if (layerObstacle is RectangleObstacle)
                        {
                            //bound check of rectangle obstacles is enough, block is ok
                            blockFilled = true;
                            break;
                        }

                        var polygonObstacle = layerObstacle as PolygonObstacle;
                        if (polygonObstacle == null)
                            break;
                        containAll = true;
                        foreach (var blockPoint in blockPoints)
                        {
                            if (!polygonObstacle.Contains(blockPoint))
                            {
                                containAll = false;
                                break;
                            }
                            //
                        }
                        if (containAll)
                        {
                            //all points are within the polygon, block is ok
                            blockFilled = true;
                            break;
                        }
                    }
                }
                //Console.WriteLine(blockFilled);
                if (blockFilled)
                    return true;
            }
            return false;
        }

        public static void CalculateBounds(IList<Vector2> convexPolygon, out float minX, out float maxX, out float minY, out float maxY)
        {
            if (convexPolygon.Count == 0)
            {
                minX = 0;
                maxX = 0;
                minY = 0;
                maxY = 0;
                return;
            }
            minX = float.MaxValue;
            maxX = float.MinValue;
            minY = float.MaxValue;
            maxY = float.MinValue;
            foreach (var polygonVertex in convexPolygon)
            {
                if (polygonVertex.X < minX)
                    minX = polygonVertex.X;
                if (polygonVertex.X > maxX)
                    maxX = polygonVertex.X;
                if (polygonVertex.Y < minY)
                    minY = polygonVertex.Y;
                if (polygonVertex.Y > maxY)
                    maxY = polygonVertex.Y;
            }
            maxX = (float)Math.Ceiling(maxX);
            maxY = (float)Math.Ceiling(maxY);
        }

        private static List<Vector2> GetBlockPoints(Block block, int layer)
        {
            var obstacles = new List<ILineObstacle>();
            block.GetCollision(obstacles, false);
            if (obstacles.Count == 0)
            {
                obstacles.Add(LineObstacle.DefaultLeft((int)block.Position.X, (int)block.Position.Y, layer));
                obstacles.Add(LineObstacle.DefaultTop((int)block.Position.X, (int)block.Position.Y, layer));
                obstacles.Add(LineObstacle.DefaultRight((int)block.Position.X, (int)block.Position.Y, layer));
                obstacles.Add(LineObstacle.DefaultBottom((int)block.Position.X, (int)block.Position.Y, layer));
            }
            var blockPoints = new List<Vector2>();
            foreach (var lineObstacle in obstacles)
            {
                if (!blockPoints.Contains(lineObstacle.Start))
                    blockPoints.Add(lineObstacle.Start);
                if (!blockPoints.Contains(lineObstacle.End))
                    blockPoints.Add(lineObstacle.End);
            }
            return blockPoints;
        }

        private static void CreateLineSegments(IList<Vector2> polygonVertices, ICollection<IObstacle> obstacles, int layer)
        {
            for (int i = 0, j = polygonVertices.Count - 1; i < polygonVertices.Count; j = i++)
                obstacles.Add(new LineObstacle(polygonVertices[i], polygonVertices[j], layer));
        }

        public void AddPolygonObstacle(List<Vector2> polygonVertices, bool isRectangle, List<IObstacle> obstacles, int layer)
        {
            if (isRectangle)
            {
                var minX = float.MaxValue;
                var maxX = float.MinValue;
                var minY = float.MaxValue;
                var maxY = float.MinValue;
                foreach (var polygonVertex in polygonVertices)
                {
                    if (polygonVertex.X < minX)
                        minX = polygonVertex.X;
                    if (polygonVertex.X > maxX)
                        maxX = polygonVertex.X;
                    if (polygonVertex.Y < minY)
                        minY = polygonVertex.Y;
                    if (polygonVertex.Y > maxY)
                        maxY = polygonVertex.Y;
                }
                var width = maxX - minX;
                var height = maxY - minY;
                var rectangle = new RectangleObstacle(new Vector2(minX, minY), layer, width, height);
                obstacles.Add(rectangle);
            }
            else
            {
                var polygonObstacle = new PolygonObstacle(layer) { Vertices = polygonVertices };
                obstacles.Add(polygonObstacle);
            }
        }
    }
}
