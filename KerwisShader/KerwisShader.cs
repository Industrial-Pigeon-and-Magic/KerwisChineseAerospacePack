using System;
using System.Collections.Generic;
using System.IO;
using UnityDDSLoader;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace KerwisShader
{
	enum MatParamType
	{
		metallic,
		smoothness,
		ambient,
		defaulttemperature,
		phongtesstrength
	}
	public class KerwisShader : PartModule
	{
		public static string DllDirectory
		{
			get
			{
				return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			}
		}

		private static AssetBundle m_shaderAB;

		private static Shader m_LitShader;

		private static Shader m_CutoffShader;

		private static Shader m_TessellationShader;
		public static AssetBundle shaderAB
		{
			get
			{
				if (m_shaderAB == null)
				{
					string name = "shader_2019418";
					if (Application.unityVersion == "2019.2.2f1")
						name = "shader_201922";
					else if (Application.unityVersion != "2019.4.18f1")
						Debug.Log("KerwisShader:Unity Version " + Application.unityVersion + " not supported");
					switch (Application.platform)
					{
						case RuntimePlatform.WindowsPlayer:
							m_shaderAB = AssetBundle.LoadFromFile(Path.Combine(DllDirectory, name + "_win64.assetbundle")); break;
						case RuntimePlatform.OSXPlayer:
							m_shaderAB = AssetBundle.LoadFromFile(Path.Combine(DllDirectory, name + "_osx.assetbundle")); break;
						case RuntimePlatform.LinuxPlayer:
							m_shaderAB = AssetBundle.LoadFromFile(Path.Combine(DllDirectory, name + "_linux64")); break;
						default:
							Debug.Log("KerwisShader:Runtime Platform " + Application.platform + " not supported"); break;
					}
				}
				if (m_shaderAB == null)
					Debug.LogError("KerwisShader:shader AB包加载失败!");
				return m_shaderAB;
			}
		}

		public static Shader LitShader
		{
			get
			{
				if (m_LitShader == null) m_LitShader = shaderAB.LoadAsset<Shader>("KerwisLit");
				if (m_LitShader == null) Debug.LogError("KerwisShader:未找到Lit Shader!");
				return m_LitShader;
			}
		}
		public static Shader CutoffShader
		{
			get
			{
				if (m_CutoffShader == null)
					m_CutoffShader = shaderAB.LoadAsset<Shader>("KerwisLitCutout");
				if (m_CutoffShader == null)
					Debug.LogError("KerwisShader:未找到Lit Cutout Shader!");
				return m_CutoffShader;
			}
		}
		public static Shader TessellationShader
		{
			get
			{
				if (m_TessellationShader == null) m_TessellationShader = shaderAB.LoadAsset<Shader>("KerwisLitTessellation");
				if (m_TessellationShader == null) Debug.LogError("KerwisShader:未找到Lit Tessellation Shader!");
				return m_TessellationShader;
			}
		}

		[KSPField(isPersistant = false)]
		public string TextureFolder = "";
#if DEBUG
		[UI_ChooseOption(scene = UI_Scene.All, options = new string[]
		{
			"None"
		})]
		[KSPField(guiName = "正在编辑材质:", isPersistant = true, guiActive = true, guiActiveEditor = true)]
		public string EditingMat = "";

		[UI_FloatRange(scene = UI_Scene.All, minValue = 0f, maxValue = 3f)]
		[KSPField(guiName = "自发光R", isPersistant = true, guiActive = true, guiActiveEditor = true)]
		public float emisR;

		[UI_FloatRange(scene = UI_Scene.All, minValue = 0f, maxValue = 3f)]
		[KSPField(guiName = "自发光G", isPersistant = true, guiActive = true, guiActiveEditor = true)]
		public float emisG;

		[UI_FloatRange(scene = UI_Scene.All, minValue = 0f, maxValue = 3f)]
		[KSPField(guiName = "自发光B", isPersistant = true, guiActive = true, guiActiveEditor = true)]
		public float emisB;

		[UI_FloatRange(scene = UI_Scene.All, minValue = 0f, maxValue = 3f)]
		[KSPField(guiName = "自发光A", isPersistant = true, guiActive = true, guiActiveEditor = true)]
		public float emisA;

		[UI_FloatRange(scene = UI_Scene.All, minValue = 0f, maxValue = 1.5f, stepIncrement = 0.05f)]
		[KSPField(guiName = "金属度", isPersistant = true, guiActive = true, guiActiveEditor = true)]
		public float metallic = 1f;

		[UI_FloatRange(scene = UI_Scene.All, minValue = 0f, maxValue = 1.5f, stepIncrement = 0.05f)]
		[KSPField(guiName = "光滑度", isPersistant = true, guiActive = true, guiActiveEditor = true)]
		public float smoothness = 1f;

		[UI_FloatRange(scene = UI_Scene.All, minValue = 0f, maxValue = 2f, stepIncrement = 0.05f)]
		[KSPField(guiName = "环境光", isPersistant = true, guiActive = true, guiActiveEditor = true)]
		public float ao = 1f;
#endif
		[KSPField]
		public string ShaderType = "";

		[KSPField]
		public string MatParams;

		[KSPField]
		public bool PhysicallyBlackBody = false;

		private Dictionary<string, List<Renderer>> materialDict;
		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			//获得所有材质球,构造字典
			materialDict = new Dictionary<string, List<Renderer>>();
			foreach (Renderer renderer in part.FindModelRenderersCached())
			{
				string key = renderer.sharedMaterial.name.Replace(" (Instance)", "");
				if (!materialDict.ContainsKey(key))
					materialDict.Add(key, new List<Renderer>());
				materialDict[key].Add(renderer);
			}
			if (materialDict.Count == 0)
			{
				LogError("未能获取到part的材质球!");
				return;
			}
            string text = "";
			foreach (string str in materialDict.Keys)
				text = text + "\n" + str;
#if DEBUG
			Log("获取到" + materialDict.Count.ToString() + "个材质球,分别为" + text);
#endif

			//获得part所有贴图的目录,将所有dds文件备案
			DirectoryInfo TexFolder = new DirectoryInfo(Path.Combine(Environment.CurrentDirectory, "GameData", TextureFolder));
			if (!TexFolder.Exists)
			{
				LogError("未找到贴图目录:" + TextureFolder);
				return;
			}
			FileInfo[] ddsFiles = TexFolder.GetFiles("*.dds");
#if DEBUG
			if (ddsFiles.Length == 0)
			{
				LogError("未在给定的目录" + TextureFolder + "中找到.dds文件.");
				return;
			}
			else
			{
				string text2 = "在" + TextureFolder + "中找到" + ddsFiles.Length + "个.dds文件:";
				foreach (FileInfo t in ddsFiles)
					text2 = text2 + "\n" + t.Name + ",";
				Log(text2);
            }
#endif
			
			//读取给Transform特别指定的shader
			string[] TransformShaderpairs = ShaderType.Replace(" ", "").Split(';');
			Dictionary<string, string> TransformShaderPairs = new Dictionary<string, string>();
			foreach(string s in TransformShaderpairs)
            {
				string[] pair = s.Split(':');
				if (pair.Length == 2)
				{
					TransformShaderPairs.Add(pair[0], pair[1]);
#if DEBUG
					Log("cfg给GameObject" + pair[0] + "指定了shader名称:" + pair[1]);
#endif
				}
			}
			
			//读取cfg中指定的材质参数
			//示例 MatParams = [TransformName]:metallic:0.7,smoothness:0.3;[TransformName]:metallic: 0.7,smoothness: 0.3; ...
			Dictionary<string, Dictionary<MatParamType, float>> TransformMatParamPairs = new Dictionary<string, Dictionary<MatParamType, float>>();
			if (!string.IsNullOrEmpty(MatParams))
			{
#if DEBUG
				Log("正在读取MatParams...");
#endif
				MatParams = MatParams.Replace(" ", "");
				string[] TransformMatParampairs = MatParams.Split(';');
				foreach (string s in TransformMatParampairs)
				{
					if (string.IsNullOrEmpty(s)) continue;
					string[] sarray1 = s.Split(',');//[TransformName]:metallic:0.7		smoothness:0.3
                    string TransformName = sarray1[0].Split(':')[0];
                    TransformMatParamPairs.Add(TransformName, new Dictionary<MatParamType, float>());
					byte isFirstToken = 1;
					foreach (string ss in sarray1)
					{
						if (string.IsNullOrEmpty(ss)) continue;
						string[] sarray2 = ss.Split(':');//[TransformName]		metallic		0.7
						MatParamType paramtype;
						switch (sarray2[isFirstToken])
						{
							case "metallic": paramtype = MatParamType.metallic; break;
							case "smoothness": paramtype = MatParamType.smoothness; break;
							case "ambient": paramtype = MatParamType.ambient; break;
							case "defaulttemperature": paramtype = MatParamType.defaulttemperature; break;
							case "phongtesstrength": paramtype = MatParamType.phongtesstrength; break;
							default:
								{
									LogError("未知的材质参数种类:" + sarray2[isFirstToken] + ".将跳过此参数.");
									continue;
								}
						}
						float value = float.Parse(sarray2[isFirstToken + 1]);
						if (isFirstToken == 1) isFirstToken = 0;
						TransformMatParamPairs[TransformName].Add(paramtype, value);
#if DEBUG
						Log("给Transform " + TransformName + "的参数" + paramtype + "指定了值:" + value);
#endif
					}
				}
			}
			//遍历每一个材质球名称
			foreach (KeyValuePair<string, List<Renderer>> keyValuePair in materialDict)
			{
				//遍历Renderer列表
				foreach (Renderer r in keyValuePair.Value)
				{
					//替换shader
					if (TransformShaderPairs.ContainsKey(r.gameObject.name))
						switch (TransformShaderPairs[r.gameObject.name])
						{
							case "cutout": r.sharedMaterial.shader = CutoffShader; break;
							case "tessellation":
								{
									r.sharedMaterial.shader = TessellationShader;
									if (TransformMatParamPairs.ContainsKey(r.gameObject.name))
										if (TransformMatParamPairs[r.gameObject.name].ContainsKey(MatParamType.phongtesstrength))
											r.sharedMaterial.SetFloat("_TessPhongStrength", TransformMatParamPairs[r.gameObject.name][MatParamType.phongtesstrength]);
									Log("给物体" + r.gameObject.name + "指定了曲面细分shader,_TessPhongStrength = " + r.sharedMaterial.GetFloat("_TessPhongStrength"));
									break;
								}
							default:
								{
									LogError("未找到Shader\"" + TransformShaderPairs[r.gameObject.name] + "\"给GameObject\"" + r.gameObject.name + "\".正在使用Lit shader.\n" +
										"KerwisShader插件现版本除了默认Shader\"Lit\"外只有两种:\"cutout\"与\"tessellation\".");
									r.sharedMaterial.shader = LitShader; break;
								}
						}
					else r.sharedMaterial.shader = LitShader;
					//如果cfg有专门指定当前Transform的材质参数
					if (TransformMatParamPairs.ContainsKey(r.gameObject.name))
					{
#if DEBUG
						Log("正在给" + r.gameObject.name + "设置参数...");
#endif
						foreach (KeyValuePair<MatParamType, float> param in TransformMatParamPairs[r.gameObject.name])
							switch (param.Key)
							{
								case MatParamType.metallic: r.sharedMaterial.SetFloat("_Metallic", param.Value); break;
								case MatParamType.smoothness: r.sharedMaterial.SetFloat("_Smoothness", param.Value); break;
								case MatParamType.ambient: r.sharedMaterial.SetFloat("_AmbientMultiplier", param.Value); break;
								case MatParamType.defaulttemperature: r.sharedMaterial.SetFloat("_Temperature", param.Value); break;
							}
					}
				}

				//遍历之前获得的dds文件列表,指定dds贴图
				foreach (FileInfo ddsInfo in ddsFiles)
				{
					if (ddsInfo.Name.Contains(keyValuePair.Key))
						if (ddsInfo.Name.Contains("BaseMap"))
							foreach (Renderer r in keyValuePair.Value)
								r.sharedMaterial.SetTexture("_BaseMap", DDSLoader.Instance.FromFile(ddsInfo.FullName));
						else if (ddsInfo.Name.Contains("MaskMap"))
							foreach (Renderer r in keyValuePair.Value)
								r.sharedMaterial.SetTexture("_MaskMap", DDSLoader.Instance.FromFile(ddsInfo.FullName));
						else if (ddsInfo.Name.Contains("Normal"))
							foreach (Renderer r in keyValuePair.Value)
								r.sharedMaterial.SetTexture("_NormalMap", DDSLoader.Instance.FromFile(ddsInfo.FullName));
						else if (ddsInfo.Name.Contains("Emissive"))
							foreach (Renderer r in keyValuePair.Value)
								r.sharedMaterial.SetTexture("_EmissiveMap", DDSLoader.Instance.FromFile(ddsInfo.FullName));
				}
			}
#if DEBUG
			Callback<BaseField, object> changeEmisCol = ChangeEmissionColor;
			Fields["emisR"].uiControlEditor.onFieldChanged = changeEmisCol;
			Fields["emisG"].uiControlEditor.onFieldChanged = changeEmisCol;
			Fields["emisB"].uiControlEditor.onFieldChanged = changeEmisCol;
			Fields["emisA"].uiControlEditor.onFieldChanged = changeEmisCol;
			Fields["emisR"].uiControlFlight.onFieldChanged = changeEmisCol;
			Fields["emisG"].uiControlFlight.onFieldChanged = changeEmisCol;
			Fields["emisB"].uiControlFlight.onFieldChanged = changeEmisCol;
			Fields["emisA"].uiControlFlight.onFieldChanged = changeEmisCol;
			Fields["metallic"].uiControlEditor.onFieldChanged = new Callback<BaseField, object>(ChangeMetallic);
			Fields["metallic"].uiControlFlight.onFieldChanged = new Callback<BaseField, object>(ChangeMetallic);
			Fields["smoothness"].uiControlEditor.onFieldChanged = new Callback<BaseField, object>(ChangeSmoothness);
			Fields["smoothness"].uiControlFlight.onFieldChanged = new Callback<BaseField, object>(ChangeSmoothness);
			Fields["ao"].uiControlEditor.onFieldChanged = new Callback<BaseField, object>(ChangeAmbientOcclusion);
			Fields["ao"].uiControlFlight.onFieldChanged = new Callback<BaseField, object>(ChangeAmbientOcclusion);
			BaseField baseField = base.Fields["EditingMat"];
			baseField.uiControlEditor.onFieldChanged = new Callback<BaseField, object>(ChangeSelectedMat);
			baseField.uiControlFlight.onFieldChanged = new Callback<BaseField, object>(ChangeSelectedMat);
			string[] array = materialDict.Keys.ToArray<string>();
			((UI_ChooseOption)baseField.uiControlEditor).options = array;
			((UI_ChooseOption)baseField.uiControlFlight).options = array;
			EditingMat = array[0];
			ChangeSelectedMat(baseField, 1f);
			Log("使用part的温度作为黑体辐射自发光参考值:" + PhysicallyBlackBody);
#endif
		}

		public override void OnUpdate()
		{
			if (PhysicallyBlackBody)
				foreach (KeyValuePair<string, List<Renderer>> pair in materialDict)
					foreach (Renderer r in pair.Value)
						r.sharedMaterial.SetFloat("_Temperature", (float)part.temperature);
		}
#if DEBUG
		private void ChangeEmissionColor(BaseField field, object oldValueObj)
		{
			List<Renderer> list;
			if (materialDict.TryGetValue(EditingMat, out list))
				foreach (Renderer r in list)
					r.sharedMaterial.SetColor("_EmissiveColor", new Color(emisR, emisG, emisB, emisA));
		}

		private void ChangeMetallic(BaseField field, object oldValueObj)
		{
			List<Renderer> list;
			if (materialDict.TryGetValue(this.EditingMat, out list))
				foreach (Renderer material in list)
					material.sharedMaterial.SetFloat("_Metallic", this.metallic);
		}

		private void ChangeSmoothness(BaseField field, object oldValueObj)
		{
            if (materialDict.TryGetValue(EditingMat, out List<Renderer> list))
                foreach (Renderer material in list)
                    material.sharedMaterial.SetFloat("_Smoothness", smoothness);
        }

		private void ChangeAmbientOcclusion(BaseField field, object oldValueObj)
		{
            if (materialDict.TryGetValue(EditingMat, out List<Renderer> list))
                foreach (Renderer material in list)
                    material.sharedMaterial.SetFloat("_AmbientMultiplier", ao);
        }

		private void ChangeSelectedMat(BaseField field, object oldValueObj)
		{
            if (materialDict.TryGetValue(EditingMat, out List<Renderer> list))
            {
                ao = list[0].sharedMaterial.GetFloat("_AmbientMultiplier");
                metallic = list[0].sharedMaterial.GetFloat("_Metallic");
                smoothness = list[0].sharedMaterial.GetFloat("_Smoothness");
                Color color = list[0].sharedMaterial.GetColor("_EmissiveColor");
                emisR = color.r;
                emisG = color.g;
                emisB = color.b;
                emisA = color.a;
            }
        }
#endif
		void Log(string message)
		{
			Debug.Log("KerwisShader at part " + part.name + ":" + message);
		}
		void LogError(string message)
		{
			Debug.LogError("KerwisShader at part " + part.name + ":" + message);
		}
	}
}