using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Gameplay
{

    /// <summary>
    ///
    /// Timer
    /// 当前定时器程序为游戏运行真实时间 不受Time.timeScale的影响 切记
    /// 
    /// Anchor: ChenJC
    /// Time: 2022/10/09
    /// Document: https://blog.csdn.net/qq_39162566/article/details/113105351
    /// </summary>
    public class EasyTimer : MonoBehaviour
    {

        //定时器数据类
        public class TimerTask
        {
            public int tag;
            public float tm;
            public float life;
            public long count;
            public Action func;
            public TimerTask Clone()
            {
                var timerTask = new TimerTask();
                timerTask.tag = tag;
                timerTask.tm = tm;
                timerTask.life = life;
                timerTask.count = count;
                timerTask.func = func;
                return timerTask;
            }
            public void Destory()
            {
                m_freeTaskCls.Enqueue( this );
            }
        }


        #region Member property

        protected static List<TimerTask> m_activeTaskCls = new List<TimerTask>();//激活中的TimerTask对象
        protected static Queue<TimerTask> m_freeTaskCls = new Queue<TimerTask>();//闲置TimerTask对象
        protected static HashSet<Action> lateChannel = new HashSet<Action>();//确保callLate调用的唯一性
        protected static int m_tagCount = 1000; //timer的唯一标识
        protected static bool m_inited = false; //初始化
        protected bool m_isBackground = false;//是否可以后台运行 false：退到后台时定时器停止运行 

        #endregion


        #region public methods

        //每帧结束时执行回调 : 当前帧内的多次调用仅在当前帧结束的时候执行一次
        public static void CallerLate( Action func )
        {
            if ( !lateChannel.Contains( func ) )
            {
                lateChannel.Add( func );
                SetTimeout( 0f, func );
            }
        }


        //delay秒后 执行一次回调
        public static int SetTimeout( float delay, Action func )
        {
            return SetInterval( delay, func, false, 1 );
        }

        /// <summary>
        /// 周期性定时器 间隔一段时间调用一次
        /// </summary>
        /// <param name="interval"> 间隔时长: 秒</param>
        /// <param name="func"> 调用的方法回调 </param>
        /// <param name="immediate"> 是否立即执行一次 </param>
        /// <param name="times"> 调用的次数: 默认永久循环 当值<=0时会一直更新调用 当值>0时 循环指定次数后 停止调用 </param>
        /// <returns></returns>
        public static int SetInterval( float interval, Action func, bool immediate = false, int times = 0 )
        {
            //从free池中 获取一个闲置的TimerTask对象
            var timer = GetFreeTimerTask();
            timer.tm = 0;
            timer.life = interval;
            timer.func = func;
            timer.count = times;
            timer.tag = ++m_tagCount;

            //尝试初始化
            Init();

            //立即执行一次
            if ( immediate )
            {
                --timer.count;
                func?.Invoke();
                if ( timer.count == 0 )
                {

                    timer.Destory();
                }
                else
                {
                    //添加到激活池中
                    m_activeTaskCls.Add( timer );
                }
            }
            else
            {
                //添加到激活池中
                m_activeTaskCls.Add( timer );
            }

            return m_tagCount;
        }

        #endregion


        #region Get Timer methods

        /// <summary>
        /// 通过Tag获取定时器对象
        /// </summary>
        /// <param name="tag"></param>
        /// <returns></returns>
        public static TimerTask GetTimer( int tag )
        {
            return m_activeTaskCls.Find( ( TimerTask t ) =>
            {
                return t.tag == tag;
            } )?.Clone();
        }

        /// <summary>
        /// 通过Tag获取定时器对象
        /// </summary>
        /// <param name="tag"></param>
        /// <returns></returns>
        public static TimerTask GetTimer( Action func )
        {
            return m_activeTaskCls.Find( ( TimerTask t ) =>
            {
                return t.func == func;
            } )?.Clone();
        }
        #endregion


        #region Clean Timer methods

        /// <summary>
        /// 通过ID 清理定时器
        /// </summary>
        /// <param name="tag">定时器标签</param>
        /// <returns></returns>
        public static void ClearTimer( int tag )
        {
            int index = m_activeTaskCls.FindIndex( ( TimerTask t ) =>
            {
                return t.tag == tag;
            } );

            if ( index != -1 )
            {
                var t = m_activeTaskCls[ index ];
                if ( lateChannel.Count != 0 && lateChannel.Contains( t.func ) )
                {
                    lateChannel.Remove( t.func );
                }
                m_activeTaskCls.RemoveAt( index );
                m_freeTaskCls.Enqueue( t );
            }
        }

        /// <summary>
        /// 通过方法 清理定时器
        /// </summary>
        /// <param name="func">处理方法</param>
        /// <returns></returns>
        public static void ClearTimer( Action func )
        {
            int index = m_activeTaskCls.FindIndex( ( TimerTask t ) =>
            {
                return t.func == func;
            } );

            if ( index != -1 )
            {
                var t = m_activeTaskCls[ index ];
                if ( lateChannel.Count != 0 && lateChannel.Contains( t.func ) )
                {
                    lateChannel.Remove( t.func );
                }
                m_activeTaskCls.RemoveAt( index );
                m_freeTaskCls.Enqueue( t );
            }
        }

        /// <summary>
        /// 清理所有定时器
        /// </summary>
        public static void ClearTimers()
        {
            lateChannel.Clear();
            m_activeTaskCls.ForEach( timer => m_freeTaskCls.Enqueue( timer ) );
            m_activeTaskCls.Clear();
        }

        #endregion


        #region System methods

        //Update更新之前
        private void Start()
        {
            DontDestroyOnLoad( gameObject );
            StopAllCoroutines();
            StartCoroutine( TimerElapse() );
        }

        //程序切换到后台
        private void OnApplicationPause( bool pause )
        {
            if ( !m_isBackground )
            {
                if ( pause )
                {
                    StopAllCoroutines();
                }
                else
                {

                    StopAllCoroutines();
                    StartCoroutine( TimerElapse() );
                }
            }
        }

        //定时器调度
        private IEnumerator TimerElapse()
        {

            TimerTask t = null;

            while ( true )
            {
                if ( m_activeTaskCls.Count > 0 )
                {
                    float dt = Time.unscaledDeltaTime;
                    for ( int i = 0; i < m_activeTaskCls.Count; ++i )
                    {
                        t = m_activeTaskCls[ i ];
                        t.tm += Time.unscaledDeltaTime;
                        if ( t.tm >= t.life )
                        {
                            t.tm -= t.life;
                            if ( t.count == 1 )
                            {
                                m_activeTaskCls.RemoveAt( i-- );
                                if ( lateChannel.Count != 0 && lateChannel.Contains( t.func ) )
                                {
                                    lateChannel.Remove( t.func );
                                }
                                t.Destory();
                            }
                            --t.count;
                            t.func();
                        }
                    }
                }
                yield return 0;
            }
        }

        //初始化
        protected static void Init()
        {
            if ( !m_inited )
            {
                m_inited = true;
                var inst = new GameObject( "TimerNode" );
                inst.AddComponent<EasyTimer>();
            }
        }

        //获取闲置定时器
        protected static TimerTask GetFreeTimerTask()
        {
            if ( m_freeTaskCls.Count > 0 )
            {
                return m_freeTaskCls.Dequeue();
            }
            return new TimerTask();
        }

        #endregion

    }


}
