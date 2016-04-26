﻿using CSharpGL.ModelAdapters;
using CSharpGL.Models;
using GLM;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CSharpGL.Demos
{
    public partial class Form01Simple
    {
        
        DragParam dragParam;

        private FormBulletinBoard mouseBoard;

        private void glCanvas1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                // operate camera
                rotator.SetBounds(this.glCanvas1.Width, this.glCanvas1.Height);
                rotator.MouseDown(e.X, e.Y);
            }
            else if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                // move vertex
                PickedGeometry pickedGeometry = RunPicking(e.X, e.Y);
                if (pickedGeometry != null)
                {
                    var dragParam = new DragParam(camera, pickedGeometry);
                    dragParam.lastFarPos = glm.unProject(new vec3(e.X, glCanvas1.Height - e.Y - 1, 1),
                        dragParam.viewMatrix, dragParam.projectionMatrix, dragParam.viewport);
                    dragParam.lastNearPos = glm.unProject(new vec3(e.X, glCanvas1.Height - e.Y - 1, 0),
                        dragParam.viewMatrix, dragParam.projectionMatrix, dragParam.viewport);
                    this.dragParam = dragParam;

                    this.lblRightMouseDown.Text = string.Format("near: [{0}] far: [{1}] depth: [{2}]",
                        dragParam.lastNearPos, dragParam.lastFarPos, dragParam.targetDepth);
                }
            }
        }

        private void glCanvas1_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                // operate camera
                rotator.MouseMove(e.X, e.Y);
                //this.cameraUpdated = true;
            }
            else if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                // move vertex
                if (this.dragParam != null)
                {
                    var farPos = glm.unProject(new vec3(e.X, glCanvas1.Height - e.Y - 1, 1),
                        dragParam.viewMatrix, dragParam.projectionMatrix, dragParam.viewport);
                    var nearPos = glm.unProject(new vec3(e.X, glCanvas1.Height - e.Y - 1, 0),
                        dragParam.viewMatrix, dragParam.projectionMatrix, dragParam.viewport);
                    vec3[] differences = new vec3[dragParam.targetDepth.Length];
                    for (int i = 0; i < differences.Length; i++)
                    {
                        differences[i] = (nearPos - dragParam.lastNearPos) * (1 - dragParam.targetDepth[i])
                            + (farPos - dragParam.lastFarPos) * dragParam.targetDepth[i];
                    }
                    dragParam.lastFarPos = farPos;
                    dragParam.lastNearPos = nearPos;

                    this.rendererDict[this.selectedModel].MovePositions(
                        differences, dragParam.pickedGeometry.Indexes);

                    this.lblRightMouseMove.Text = string.Format("near: [{0}] far: [{1}] diff: [{2}]",
                        nearPos, farPos, differences);
                }
                else
                { this.mouseBoard.SetContent("Mouse Move: No action."); }
            }
            else
            {
                RunPicking(e.X, e.Y);
            }
        }

        private void glCanvas1_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                // operate camera
                rotator.MouseUp(e.X, e.Y);
            }
            else if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                // move vertex
                //this.pickedGeometry = null;
                this.dragParam = null;

                this.mouseBoard.SetContent("Mouse Up: No action.");
            }
        }

        private PickedGeometry RunPicking(int x, int y)
        {
            lock (this.synObj)
            {
                {
                    this.glCanvas1_OpenGLDraw(selectedModel, null);
                    Color c = GL.ReadPixel(x, this.glCanvas1.Height - y - 1);
                    c = Color.FromArgb(255, c);
                    this.lblReadColor.BackColor = c;
                    var depth = new UnmanagedArray<float>(1);
                    GL.ReadPixels(x, glCanvas1.Height - y - 1, 1, 1,
                        GL.GL_DEPTH_COMPONENT, GL.GL_FLOAT, depth.Header);
                    var targetDepth = depth[0];
                    depth.Dispose();
                    this.lblText.Text = string.Format(
                        "Position: {0}, {1}, Depth: {2}",
                            new Point(x, y), this.lblReadColor.BackColor, targetDepth);
                }
                {
                    IColorCodedPicking pickable = this.rendererDict[this.SelectedModel];
                    pickable.MVP = this.camera.GetProjectionMat4() * this.camera.GetViewMat4();
                    PickedGeometry pickedGeometry = ColorCodedPicking.Pick(
                        this.camera, x, y, this.glCanvas1.Width, this.glCanvas1.Height, pickable);
                    if (pickedGeometry != null)
                    {
                        this.RunPickingBoard.SetContent(pickedGeometry.ToString(
                            camera.GetProjectionMat4(), camera.GetViewMat4()));
                    }
                    else
                    {
                        this.RunPickingBoard.SetContent("picked nothing.");
                    }

                    return pickedGeometry;
                }
            }
        }

        class DragParam
        {
            public DragParam(Camera camera, PickedGeometry pickedGeometry)
            {
                this.pickedGeometry = pickedGeometry;
                this.projectionMatrix = camera.GetProjectionMat4();
                this.viewMatrix = camera.GetViewMat4();
                var viewport = new int[4]; GL.GetInteger(GetTarget.Viewport, viewport);
                this.viewport = new vec4(viewport[0], viewport[1], viewport[2], viewport[3]);

                this.targetDepth = new float[pickedGeometry.Positions.Length];
                for (int i = 0; i < pickedGeometry.Positions.Length; i++)
                {
                    var worldPos = new vec3(projectionMatrix * viewMatrix * new vec4(pickedGeometry.Positions[i], 1));
                    vec3 projectedPos = glm.project(worldPos, viewMatrix, projectionMatrix, this.viewport);
                    vec3 win = new vec3(projectedPos.x, projectedPos.y, 1);
                    vec3 farPos = glm.unProject(win,
                        viewMatrix, projectionMatrix, this.viewport);
                    win.z = 0;
                    //vec3 nearPos = glm.unProject(win,
                    //    dragParam.viewMatrix, dragParam.projectionMatrix, dragParam.viewport);
                    vec3 vTarget = worldPos - camera.Position;//nearPos;
                    vec3 vFar = farPos - camera.Position;// nearPos;
                    float x = vTarget.x / vFar.x;
                    float y = vTarget.y / vFar.y;
                    float z = vTarget.z / vFar.z;
                    this.targetDepth[i] = x / 3 + y / 3 + z / 3;
                }
            }

            public vec3 lastFarPos;
            public vec3 lastNearPos;
            public float[] targetDepth;
            public mat4 projectionMatrix;
            public mat4 viewMatrix;
            public vec4 viewport;
            public PickedGeometry pickedGeometry;
        }
    }
}
