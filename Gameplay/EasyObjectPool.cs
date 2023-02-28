/**
 * 
 * class: EasyObjectPool
 * 
 * A lightweight object pool.
 * 
 * You need to create an object in the scene and then hang it.
 *
 * Support automatic capacity expansion.
 *  
 * Support recycling detection.
 * ————————————————
 * 版权声明：本文为CSDN博主「极客柒」的原创文章，遵循CC 4.0 BY-SA版权协议，转载请附上原文出处链接及本声明。
 * 原文链接：https://blog.csdn.net/qq_39162566/article/details/128290119
 * 
 */
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR 
using UnityEditor;
#endif

namespace Gameplay
{
    public class EasyObjectPool : MonoBehaviour
    {

        public class TransformPool
        {
            /// <summary>
            /// 模板预设
            /// </summary>
            /// <param name="prefab"> 预设 </param>
            /// <param name="parent"> 指定一个父类 </param>
            public TransformPool( GameObject prefab, Transform parent = null )
            {
                this.prefab = prefab;
                this.parent = parent;
            }

            ////获取预设name(KEY)
            //public string name
            //{
            //    get
            //    {
            //        if ( this.prefab )
            //            return this.prefab.name;
            //        return string.Empty;
            //    }
            //    set
            //    {
            //        if ( this.prefab )
            //            this.prefab.name = value;
            //    }
            //}

            private Queue<Transform> free = new Queue<Transform>();//闲置链表
            private List<Transform> active = new List<Transform>();//激活链表
            private GameObject prefab;//预设模板
            private Transform parent;//父节点
            private float expandTimeSinceStartup = 0f; //扩充时间
            private int expandCount = 10;//扩充基数
            private int tryExpandCount = 0;//尝试扩容的次数
                                           //动态扩容
            private void AutoExpandImmediately()
            {
                //0.01秒以内发生多次扩充
                if ( Time.realtimeSinceStartup - expandTimeSinceStartup < 1e-2 )
                {
                    expandCount = expandCount * 10;
                }
                else
                {
                    expandCount = 10;
                }
                //扩充
                Reserve( expandCount );
            }

            /// <summary>
            /// 激活对象
            /// 
            /// 从闲置链表中推出一个闲置对象 并将其加入激活列表的中
            /// 通常情况下 你只需要处理激活列表中的对象即可
            /// 
            /// </summary>
            /// <returns> 你需要为他设置父类 并为它设置 Active为true </returns>
            public Transform Pop()
            {
                if ( free.Count > 0 )
                {
                    //申请成功
                    tryExpandCount = 0;
                    var freeObj = free.Dequeue();
                    active.Add( freeObj );
                    return freeObj;
                }

                //申请扩充内存失败
                if ( tryExpandCount > 5 )
                {
                    //#if UNITY_EDITOR || ENABLE_LOG
                    //                //开始暴力GC扩充
                    //                System.GC.Collect( 0, System.GCCollectionMode.Forced, true, true );
                    //                return Pop();
                    //#endif
                    return null;
                }

                //扩容
                ++tryExpandCount;
                AutoExpandImmediately();
                return Pop();
            }

            /// <summary>
            /// 限制对象
            /// </summary>
            /// <param name="obj"></param>
            public void Push_Back( Transform obj )
            {
                if ( null != obj )
                {
                    obj.transform.SetParent( parent );
                    obj.gameObject.SetActive( false );
                    if ( active.Contains( obj ) )
                    {
                        active.Remove( obj );
                    }
                    if ( !free.Contains( obj ) )
                    {
                        free.Enqueue( obj );
                    }
                }
            }


            public enum PoolElementState
            {
                Unknown = 0,//未知 不存在当前对象池中
                Active, //激活状态
                Free, //闲置状态
            }

            /// <summary>
            /// 获取对象在池中的状态
            /// </summary>
            /// <param name="dest"> enum: PoolElementState </param>
            /// <returns></returns>
            public PoolElementState GetElementState( Transform dest )
            {
                if ( free.Contains( dest ) )
                {
                    return PoolElementState.Free;
                }

                if ( active.Contains( dest ) )
                {
                    return PoolElementState.Active;
                }

                return PoolElementState.Unknown;
            }

            /// <summary>
            /// 是否在对象池
            /// </summary>
            /// <param name="obj"></param>
            /// <returns></returns>
            public bool InSidePool( Transform obj )
            {
                return PoolElementState.Unknown != GetElementState( obj );
            }

            /// <summary>
            /// 回收所有激活的对象
            /// </summary>
            public void Recycle()
            {
                var objs = active.ToArray();
                for ( int i = 0; i < objs.Length; i++ )
                {
                    if ( objs[ i ] != null )
                    {
                        Push_Back( objs[ i ] );
                    }
                }

                objs = free.ToArray();
                for ( int i = 0; i < objs.Length; i++ )
                {
                    if ( objs[ i ] != null )
                    {
                        Push_Back( objs[ i ] );
                    }
                }
            }

            /// <summary>
            /// 预定一定数量的预设
            /// </summary>
            /// <param name="count"></param>
            public void Reserve( int count )
            {
                string key = prefab.name;
                for ( int i = 0; i < count; i++ )
                {
                    var inst = GameObject.Instantiate( prefab, parent );
                    inst.SetActive( false );
                    inst.name = $"{ key } <Clone>";
                    free.Enqueue( inst.transform );
                    nameofDict.Add( inst.transform, key );
                }
                expandTimeSinceStartup = Time.realtimeSinceStartup;
            }

            /// <summary>
            /// 此操作会完全释放对象的内存占用 是delete哦~
            /// </summary>
            public void Release()
            {
                foreach ( var obj in free )
                {
                    Destroy( obj.gameObject, 0.0016f );
                }
                foreach ( var obj in active )
                {
                    Destroy( obj.gameObject, 0.0016f );
                }
                free.Clear();
                active.Clear();
            }

            /// <summary>
            /// 获取当前闲置数量
            /// </summary>
            /// <returns></returns>
            public int FreeCount { get { return free.Count; } }

            /// <summary>
            /// 获取当前池子容量
            /// </summary>
            public int Capacity { get { return active.Count + free.Count; } }
        }

        /// <summary>
        /// 预设配置
        /// </summary>
        [System.Serializable]
        public class PreloadConfigs
        {
            [Header( "初始预设数量" )]
            public GameObject prefab;
            public int preloadCount = 100;
            [Header( "预制体路径( 自动生成 )" )]
            [ReadOnly]
            public string url = string.Empty;
        }

        [SerializeField]
        private List<PreloadConfigs> preloadConfigs = new List<PreloadConfigs>();
        private Dictionary<string, TransformPool> poolDict = new Dictionary<string, TransformPool>();
        private static Dictionary<Transform, string> nameofDict = new Dictionary<Transform, string>();
        private static EasyObjectPool instance = null;
        public static EasyObjectPool GetInstance() { return instance; }
        /// <summary>
        /// 首次加载是否完成
        /// </summary>
        public static bool firstPreloadFinish
        {
            get
            {
                return instance != null && instance.preloadConfigs.Count == 0;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            foreach ( var config in preloadConfigs )
            {
                if ( UnityEditor.PrefabUtility.IsPartOfPrefabAsset( config.prefab ) )
                {
                    string url = UnityEditor.AssetDatabase.GetAssetPath( config.prefab );
                    config.url = url;
                }
            }
        }
#endif

        private void Awake()
        {
            if ( instance != null && instance != this )
            {
                DestroyImmediate( gameObject );
                return;
            }
            instance = this;
            //DontDestroyOnLoad( gameObject );

            //第一次扩充
            foreach ( var config in preloadConfigs )
            {
                Add( config.prefab, config.preloadCount );
            }
            preloadConfigs.Clear();
        }

        /// <summary>
        /// 从对象池中拿一个闲置的对象
        /// </summary>
        /// <param name="key"></param>
        /// <returns>当返回null时 说明不存在这个预设的池子 你可以使用 GeneratePool 来添加一个新的池子 </returns>
        public Transform Spawn( string key )
        {

            TransformPool res = null;
            if ( poolDict.TryGetValue( key, out res ) )
            {
                Transform trans = res.Pop();
                trans.gameObject.SetActive( true );
                return trans;
            }
            return null;
        }
        /// <summary>
        /// 回收一个对象到对象池中
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public bool Despawn( Transform obj )
        {
            if ( null == obj )
            {
#if UNITY_EDITOR || ENABLE_LOG
                throw new System.Exception( "Despawn obj is null" );
#else
            return false;
#endif
            }

            if ( nameofDict.TryGetValue( obj, out string name ) && poolDict.TryGetValue( name, out TransformPool res ) )
            {
                res.Push_Back( obj );
                return true;
            }
            else
            {
                //容错处理
#if UNITY_EDITOR || ENABLE_LOG
                obj.gameObject.SetActive( false );
                Debug.LogError( $"current object is not objectPool element: {obj.name}", obj.gameObject );
#else
            Destroy( obj.gameObject );
#endif
            }
            return false;
        }


        /// <summary>
        /// 将指定key的缓存池内所有的对象全部回收
        /// </summary>
        /// <param name="pool"></param>
        public void Despawn( string pool )
        {
            if ( poolDict.TryGetValue( pool, out TransformPool res ) )
            {
                res.Recycle();
            }
#if UNITY_EDITOR || ENABLE_LOG
            else
            {
                Debug.LogError( $"exclusive pool: {pool}" );
            }
#endif
        }

        /// <summary>
        /// 延迟回收
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="delay"></param>
        public void Despawn( Transform obj, float delay )
        {
            EasyTimer.SetTimeout( delay, () => Despawn( obj ) );
        }

        /// <summary>
        /// 是否是对象池元素
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public bool Contains( Transform element )
        {
            TransformPool pool;
            if ( null != element && nameofDict.TryGetValue( element, out string name ) && poolDict.TryGetValue( name, out pool ) && pool.InSidePool( element ) )
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 回收自身所有的对象池元素 ( 包含自身 )
        /// </summary>
        /// <param name="root"></param>
        public void DespawnSelfAny<T>( Transform root ) where T : Component
        {
            T[] suspectObjects = root.GetComponentsInChildren<T>();
            foreach ( var obj in suspectObjects )
            {
                if ( Contains( obj.transform ) )
                {
                    Despawn( obj.transform );
                }
            }
        }


        /// <summary>
        /// 回收自己的子节点 如果子节点是对象池元素的话
        /// </summary>
        /// <param name="root"> 父节点 </param>
        /// <param name="includeSelf"> 本次回收是否包含父节点 </param>
        /// <param name="force"> true: 遍历所有的孩子节点  false: 仅遍历一层 </param>
        public void DespawnChildren( Transform root, bool includeSelf = false, bool force = false )
        {
            List<Transform> children = null;

            if ( force )
            {
                Transform[] suspectObjects = root.GetComponentsInChildren<Transform>();
                children = new List<Transform>( suspectObjects );
                if ( !includeSelf ) children.Remove( root );

            }
            else
            {
                children = new List<Transform>();
                if ( includeSelf )
                {
                    children.Add( root );
                }
                foreach ( Transform child in root )
                {
                    children.Add( child );
                }
            }

            foreach ( var child in children )
            {
                Despawn( child );
            }
        }

        /// <summary>
        /// 新增要给对象池
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="firstExpandCount"></param>
        public void Add( GameObject prefab, int firstExpandCount = 100 )
        {
            if ( prefab != null )
            {
                var key = prefab.name;
                if ( poolDict.ContainsKey( key ) )
                {
                    Debug.LogError( $"Add Pool Error: pool name <{key}> already exist!" );
#if UNITY_EDITOR
                    Selection.activeGameObject = prefab;
#endif
                    return;
                }
                var pool = new TransformPool( prefab, transform );
                poolDict.Add( key, pool );
                pool.Reserve( firstExpandCount );
#if UNITY_EDITOR || ENABLE_LOG
                Debug.Log( $"<color=#00ff44>[EasyObjectPool]\t对象池创建成功: {key}\t当前闲置数量: {firstExpandCount}</color>" );
#endif
            }
            else
            {
                Debug.LogError( $"Add Pool Error: prefab is null" );
            }
        }


        /// <summary>
        /// 回收所有激活对象
        /// </summary>
        public void Recycle()
        {
            foreach ( var pool in poolDict )
            {
                pool.Value.Recycle();
            }
        }

    }


    public class ReadOnlyAttribute : PropertyAttribute
    {

    }

#if UNITY_EDITOR
    [CustomPropertyDrawer( typeof( ReadOnlyAttribute ) )]
    public class ReadOnlyDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight( SerializedProperty property, GUIContent label )
        {
            return EditorGUI.GetPropertyHeight( property, label, true );
        }

        public override void OnGUI( Rect position, SerializedProperty property, GUIContent label )
        {
            GUI.enabled = false;
            EditorGUI.PropertyField( position, property, label, true );
            GUI.enabled = true;
        }

    }
#endif

}
