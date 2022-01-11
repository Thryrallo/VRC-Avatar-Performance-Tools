#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace Thry.AvatarHelpers
{
    public class Thryformance : MonoBehaviour
    {
        // Start is called before the first frame update
        void Start()
        {
        }

        public GameObject avatar;
        public ThryFormanceManager evaluator;
        bool started = false;

        int state = 0;
        List<GameObject> baseLineAvatars;
        List<GameObject> avatarCopies;
        // Update is called once per frame

        List<float> currentTimes = new List<float>();
        Queue<float> realtimeScores = new Queue<float>(new float[400]);
        float evalStart;
        int evalReturnState;
        float evalDuration;
        float evalOffset;
        void StartEvaluation(int callbackstate, float time, float offset)
        {
            currentTimes.Clear();
            evalStart = Time.time;
            evalReturnState = callbackstate;
            evalDuration = time;
            evalOffset = offset;
            state = 10;
        }

        float baseline = 0;

        public bool doRealtime;

        GameObject selectedObj;
        bool prevActive;

        public bool doAvatar;
        public (Material,float)[] materials;

        List<GameObject> cubes;
        int currentIndex = -1;
        void Update()
        {
            if (started)
            {
                if(state == 10)
                {
                    if (Time.time - evalStart > evalOffset)
                        currentTimes.Add(Time.deltaTime);
                    if (Time.time - evalStart > evalOffset + evalDuration)
                    {
                        state = evalReturnState;
                    }
                }

                if (doAvatar)
                {
                    if (Selection.activeGameObject != null)
                    {
                        if (Selection.activeGameObject == selectedObj)
                        {
                            if (prevActive != selectedObj.activeSelf)
                            {
                                int index = -1;
                                for (int i = 0; i < avatar.transform.childCount; i++)
                                    if (avatar.transform.GetChild(i).gameObject == selectedObj)
                                        index = i;
                                if (index != -1)
                                {
                                    foreach (GameObject o in avatarCopies)
                                        o.transform.GetChild(index).gameObject.SetActive(selectedObj.activeSelf);
                                }
                            }
                        }
                        selectedObj = Selection.activeGameObject;
                        prevActive = selectedObj.activeSelf;
                    }

                    if (state == 0)
                    {
                        avatar.SetActive(false);
                        string guid = AssetDatabase.FindAssets("Thryformance_Comparator t:prefab")[0];
                        GameObject baseLineAvatar = GameObject.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)));
                        baseLineAvatars = DuplicateAvatar(baseLineAvatar);
                        StartEvaluation(1, 10, 1);
                    }
                    else if (state == 1)
                    {
                        baseline = currentTimes.Average();

                        foreach (GameObject o in baseLineAvatars) o.SetActive(false);
                        avatar.SetActive(true);
                        avatarCopies = DuplicateAvatar(avatar);

                        if (doRealtime)
                        {
                            state = 3;
                        }
                        else
                        {
                            StartEvaluation(2, 10, 1);
                        }
                    }
                    else if (state == 2)
                    {
                        //evalResults[currentAvatarEval].Item2 = currentTimes.OrderBy(n => n).ElementAt(currentTimes.Count() / 2);
                        float avatarResult = currentTimes.Average();

                        evaluator.thryformance = Score(avatarResult);
                        EditorApplication.ExitPlaymode();
                    }
                    else if (state == 3)
                    {
                        realtimeScores.Dequeue();
                        realtimeScores.Enqueue(Time.deltaTime);
                        evaluator.thryformance = Score(realtimeScores.Average());
                        evaluator.ui.Repaint();
                    }
                }
                else
                {
                    if(state == 0)
                    {
                        avatar.SetActive(false);
                        cubes = new List<GameObject>();
                        for(float x = -10; x <= 10; x+=0.5f)
                        {
                            for(float y = -5; y <= 5; y+=0.5f)
                            {
                                GameObject o = GameObject.CreatePrimitive(PrimitiveType.Cube);
                                o.transform.position = new Vector3(x, y, 1);
                                o.transform.localScale = new Vector3(0.5f, 0.5f, 1);
                                cubes.Add(o);
                            }
                        }
                        StartEvaluation(1, 1, 0.2f);
                    }else if(state == 1)
                    {
                        if (currentIndex > -1) materials[currentIndex].Item2 = currentTimes.Average() / baseline;
                        else baseline = currentTimes.Average();

                        currentIndex++;

                        if (currentIndex == materials.Length)
                        {
                            evaluator.materialPerformance = materials.OrderBy(m => m.Item2).Reverse().ToArray();
                            EditorApplication.ExitPlaymode();
                        }
                        else
                        {
                            foreach(GameObject o in cubes) o.GetComponent<MeshRenderer>().material = materials[currentIndex].Item1;
                            StartEvaluation(1, 1, 0.2f);
                        }
                    }
                }
            }
            else if(EditorApplication.isPlaying)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    GameObject[] objects = SceneManager.GetSceneAt(i).GetRootGameObjects();
                    foreach (GameObject o in objects)
                        if (o != avatar && o != gameObject)
                            o.SetActive(false);
                }

                GameObject lO = new GameObject("Light");
                Light l = lO.AddComponent<Light>();
                l.type = LightType.Directional;
                l.shadows = LightShadows.Soft;
                lO.transform.rotation = Quaternion.Euler(90, 0, 0);
                GameObject cObject = new GameObject("Camera");
                cObject.AddComponent<AudioListener>();
                Camera c = cObject.AddComponent<Camera>();
                cObject.transform.position = Vector3.forward * 10;
                cObject.transform.rotation = Quaternion.Euler(0, 180, 0);

                started = true;
            }
        }

        float Score(float value)
        {
            Debug.Log(baseline + " / " + value + " = "+ (baseline / value));
            return Mathf.Min(1, baseline / value) * 100;
        }

        List<GameObject> DuplicateAvatar(GameObject avatar)
        {
            List<GameObject> list = new List<GameObject>();
            list.Add(avatar);
            float lowestY = -3;

            avatar.transform.position = new Vector3(0, lowestY, 0);
            avatar.transform.rotation = Quaternion.identity;

            for (float y = lowestY; y < 2; y += 2)
            {
                for (int s = -1; s < 2; s += 2)
                {
                    for (int i = 1; i < 8; i++)
                    {
                        GameObject copy = GameObject.Instantiate(avatar);
                        copy.transform.position = new Vector3(0.5f * s * i, y, 0.5f * i);
                        list.Add(copy);
                    }
                }
                if(y != lowestY)
                {
                    GameObject middleCopy = GameObject.Instantiate(avatar);
                    middleCopy.transform.position = new Vector3(0, y, 0);
                    list.Add(middleCopy);
                }
            }
            return list;
        }
    }
}
#endif