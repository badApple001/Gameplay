using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System;
using System.Reflection;
using Gameplay;

namespace GameCustomInspector
{

    [CanEditMultipleObjects, CustomEditor( typeof( EasyObjectPool ) )]
    public class EasyObjectPoolInspector : Editor
    {

        private Material material;
        private float horizontalIncrement = 100f;
        private float verticalIncrement = 100f;
        private EasyObjectPool _target { get { return target as EasyObjectPool; } }
        private SerializedProperty preloadConfigs_serializedProperty;
        private Dictionary<string, List<float>> lines = new Dictionary<string, List<float>>();
        GUIStyle detailStyle;
        GUIStyle titleStyle;
        private void OnEnable()
        {
            // Find the "Hidden/Internal-Colored" shader, and cache it for use.
            material = new Material( Shader.Find( "Hidden/Internal-Colored" ) );
            //line infos   
            lines.Clear();
            //label font style
            detailStyle = new GUIStyle();
            detailStyle.font = Font.CreateDynamicFontFromOSFont( "Consolas", 14 );
            detailStyle.fontStyle = FontStyle.Normal;
            titleStyle = new GUIStyle();
            titleStyle.font = Font.CreateDynamicFontFromOSFont( "仿宋", 16 );
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.normal.textColor = Color.white;
        }

        private void DrawVelocityGraph( List<float> velocityValues, float minimumVelocity,
        float maximumVelocity )
        {
            float velocityValue = velocityValues[ 0 ];
            float increment = 0;
            Vector3 lineStart = Vector3.zero;
            Vector3 lineEnd = new Vector3( horizontalIncrement, velocityValue * verticalIncrement );

            GL.Begin( GL.LINES );

            for ( int i = 1; i < velocityValues.Count; i++ )
            {
                if ( velocityValue == velocityValues[ i ]
                    && ( velocityValue == minimumVelocity || velocityValue == maximumVelocity ) )
                {
                    Handles.color = Color.red;
                }
                else
                {
                    velocityValue = velocityValues[ i ];
                    Handles.color = Color.green;
                }

                lineStart = lineEnd;
                lineEnd.x += increment;
                lineEnd.y = velocityValue * verticalIncrement;

                GL.Vertex( lineStart );
                GL.Vertex( lineEnd );
            }

            GL.End();
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.BeginVertical();

            //扩展
            Type type = typeof( EasyObjectPool );
            var info = type.GetField( "preloadConfigs", BindingFlags.NonPublic | BindingFlags.Instance );
            var preloadConfigs = info.GetValue( _target ) as IList;
            if ( preloadConfigs.Count != 0 )
            {
                if ( null == preloadConfigs_serializedProperty ) preloadConfigs_serializedProperty = serializedObject.FindProperty( "preloadConfigs" );
                EditorGUILayout.PropertyField( preloadConfigs_serializedProperty, new GUIContent( "预设配置" ) );

                if ( GUILayout.Button( "首尾交换" ) )
                {
                    var last = preloadConfigs[ preloadConfigs.Count - 1 ];
                    preloadConfigs.Remove( last );
                    preloadConfigs.Insert( 0, last );
                    info.SetValue( _target, preloadConfigs );
                }
                serializedObject.ApplyModifiedProperties();
            }
            else
            {


                info = type.GetField( "poolDict", BindingFlags.NonPublic | BindingFlags.Instance );
                var poolDict = info.GetValue( _target ) as Dictionary<string, EasyObjectPool.TransformPool>;

                if ( null != poolDict )
                {

                    //upper font normal style
                    var upStyle = new GUIStyle();
                    upStyle.alignment = TextAnchor.UpperCenter;
                    upStyle.normal.textColor = Color.red;

                    //lower font normal style
                    var lowerStyle = new GUIStyle();
                    lowerStyle.alignment = TextAnchor.LowerCenter;
                    lowerStyle.normal.textColor = Color.green;

                    GUILayout.Label( "对象池 内存监视", titleStyle );
                    GUILayout.Space( 10 );
                    foreach ( var pool in poolDict )
                    {
                        //detailStyle.normal.textColor = pool.Value.FreeCount * 1.0f / pool.Value.Capacity > 0.5f ? Color.green : Color.red;
                        //GUILayout.Label( $"{pool.Key}: {pool.Value.FreeCount}/{pool.Value.Capacity}", detailStyle );
                        //GUILayout.Space( 4 );

                        #region TODO: Draw Curve line


                        List<float> vertexs = null;
                        if ( !lines.TryGetValue( pool.Key, out vertexs ) )
                        {
                            vertexs = new List<float>() { 0 };
                            lines.Add( pool.Key, vertexs );
                        }
                        vertexs.Add( pool.Value.FreeCount * 1.0f / pool.Value.Capacity );

                        GUILayout.Label( $"{pool.Key}" );

                        // Begin to draw a horizontal layout, using the helpBox EditorStyle
                        GUILayout.BeginHorizontal( EditorStyles.helpBox );


                        //GUI.Label( new Rect( 0, layoutRectangle.height * 0.5f, 100, 20 ), "count" );
                        GUILayout.BeginVertical();

                        GUILayout.Label( pool.Value.Capacity.ToString(), upStyle, GUILayout.MinHeight( 200 * 0.334f ) );

                        GUILayout.Label( ( pool.Value.Capacity * 0.5f ).ToString(), GUILayout.MinHeight( 200 * 0.334f ) );

                        GUILayout.Label( "0", lowerStyle, GUILayout.MinHeight( 200 * 0.334f ) );

                        GUILayout.EndVertical();

                        // Reserve GUI space with a width from 10 to 10000, and a fixed height of 200, and 
                        // cache it as a rectangle.
                        Rect layoutRectangle = GUILayoutUtility.GetRect( 10, 10000, 200, 200 );


                        if ( Event.current.type == EventType.Repaint )
                        {
                            // If we are currently in the Repaint event, begin to draw a clip of the size of 
                            // previously reserved rectangle, and push the current matrix for drawing.
                            GUI.BeginClip( layoutRectangle );
                            GL.PushMatrix();

                            // Clear the current render buffer, setting a new background colour, and set our
                            // material for rendering.
                            GL.Clear( true, false, Color.black );
                            material.SetPass( 0 );

                            // Start drawing in OpenGL Quads, to draw the background canvas. Set the
                            // colour black as the current OpenGL drawing colour, and draw a quad covering
                            // the dimensions of the layoutRectangle.
                            GL.Begin( GL.QUADS );
                            GL.Color( Color.black );
                            GL.Vertex3( 0, 0, 0 );
                            GL.Vertex3( layoutRectangle.width, 0, 0 );
                            GL.Vertex3( layoutRectangle.width, layoutRectangle.height, 0 );
                            GL.Vertex3( 0, layoutRectangle.height, 0 );
                            GL.End();

                            // Start drawing in OpenGL Lines, to draw the lines of the grid.
                            GL.Begin( GL.LINES );


                            //Store measurement values to determine the offset, for scrolling animation,
                            //and the line count, for drawing the grid.

                            int offset = ( Time.frameCount * 2 ) % 50;
                            int count = ( int ) ( layoutRectangle.width / 10 ) + 20;

                            for ( int i = 0; i < count; i++ )
                            {
                                // For every line being drawn in the grid, create a colour placeholder; if the
                                // current index is divisible by 5, we are at a major segment line; set this
                                // colour to a dark grey. If the current index is not divisible by 5, we are
                                // at a minor segment line; set this colour to a lighter grey. Set the derived
                                // colour as the current OpenGL drawing colour.
                                Color lineColour = ( i % 5 == 0
                                    ? new Color( 0.5f, 0.5f, 0.5f ) : new Color( 0.2f, 0.2f, 0.2f ) );
                                GL.Color( lineColour );

                                // Derive a new x co-ordinate from the initial index, converting it straight
                                // into line positions, and move it back to adjust for the animation offset.
                                float x = i * 10 - offset;

                                if ( x >= 0 && x < layoutRectangle.width )
                                {
                                    // If the current derived x position is within the bounds of the
                                    // rectangle, draw another vertical line.
                                    GL.Vertex3( x, 0, 0 );
                                    GL.Vertex3( x, layoutRectangle.height, 0 );
                                }

                                if ( i < layoutRectangle.height / 10 )
                                {
                                    // Convert the current index value into a y position, and if it is within
                                    // the bounds of the rectangle, draw another horizontal line.
                                    GL.Vertex3( 0, i * 10, 0 );
                                    GL.Vertex3( layoutRectangle.width, i * 10, 0 );
                                }
                            }

                            //the layout recttangle pixel width of each cell
                            float step = layoutRectangle.width / 10;

                            //base offset of vertexs
                            offset = vertexs.Count - 10;

                            for ( int i = 0; i < 10; i++ )
                            {
                                //real index
                                int i1 = Mathf.Clamp( i + offset, 0, vertexs.Count );
                                int i2 = Mathf.Clamp( i + offset + 1, 0, vertexs.Count - 1 );

                                //renderer line color
                                Color lineColor = vertexs[ i1 ] < 0.5f ? Color.red : Color.green;
                                GL.Color( lineColor );
                                GL.Vertex3( i * step, layoutRectangle.height * vertexs[ i1 ], 0 );
                                GL.Vertex3( ( i + 1 ) * step, layoutRectangle.height * vertexs[ i2 ], 0 );
                            }

                            // End lines drawing.
                            GL.End();

                            // Draws the object pool occupancy status
                            //DrawVelocityGraph( vertexs, 0f, 1f );

                            // Pop the current matrix for rendering, and end the drawing clip.
                            GL.PopMatrix();
                            GUI.EndClip();
                        }

                        //End our horizontal
                        GUILayout.EndHorizontal();

                        #endregion


                        //remove front 80 element
                        if ( vertexs.Count > 100 )
                        {
                            vertexs.RemoveRange( 0, 80 );
                        }
                    }
                }
            }
            EditorGUILayout.EndVertical();
        }
    }

}
