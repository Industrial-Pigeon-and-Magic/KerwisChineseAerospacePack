using System;
using System.Collections.Generic;
using System.IO;
using Kerwis.DDSLoader;
using System.Reflection;
using UnityEngine;
using System.Linq;

namespace KerwisShader
{
	enum MatParamType
	{
		metallic,
		smoothness,
		ambient,
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

		private static Shader m_TransparentShader;

		private static Shader m_BlackBodyShader;
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
							m_shaderAB = AssetBundle.LoadFromFile(Path.Combine(DllDirectory, name + "_linux64.assetbundle")); break;
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
				if (m_LitShader == null)
				{
					m_LitShader = shaderAB.LoadAsset<Shader>("KerwisLit");
					if (m_LitShader == null)
						Debug.LogError("KerwisShader:未找到Lit Shader!");
				}
				return m_LitShader;
			}
		}
		public static Shader CutoffShader
		{
			get
			{
				if (m_CutoffShader == null)
				{
					m_CutoffShader = shaderAB.LoadAsset<Shader>("KerwisLitCutout");
					if (m_CutoffShader == null)
						Debug.LogError("KerwisShader:未找到Lit Cutout Shader!");
				}
				return m_CutoffShader;
			}
		}
		public static Shader TransparentShader
		{
			get
			{
				if (m_TransparentShader == null)
				{
					m_TransparentShader = shaderAB.LoadAsset<Shader>("KerwisLitTransparent");
					if (m_TransparentShader == null)
						Debug.LogError("KerwisShader:未找到Lit Transparent Shader!");
				}
				return m_TransparentShader;
			}
		}
		public static Shader BlackBodyShader
		{
			get
			{
				if (m_BlackBodyShader == null)
				{
					m_BlackBodyShader = shaderAB.LoadAsset<Shader>("KerwisLitBlackBody");
					if (m_BlackBodyShader == null)
						Debug.LogError("KerwisShader:未找到Lit BlackBody Shader!");
				}
				return m_BlackBodyShader;
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

		[UI_ChooseOption(scene = UI_Scene.Editor, options = new string[] { })]
		[KSPField(guiName = "#autoLOC_KS_using_Texture_Variant", isPersistant = true, guiActive = true, guiActiveEditor = true)]
		public string CurrentTextureVariant = "";

		[KSPField]
		public string ShaderType = "";

		[KSPField]
		public string MatParams = "";

		[KSPField]
		public string MatTexMapping = "";

		[KSPField]
		public string TextureVariantKeys = "";

		private Dictionary<string, List<Renderer>> materialDict;

		Dictionary<string, string> matTexMapping;

		FileInfo[] ddsFiles;
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
#if DEBUG
            string text = "";
			foreach (string str in materialDict.Keys)
				text = text + "\n" + str;
			Log("获取到" + materialDict.Count.ToString() + "个材质球,分别为" + text);
#endif

			//获得part所有贴图的目录,将所有dds文件备案
			DirectoryInfo TexFolder = new DirectoryInfo(Path.Combine(Environment.CurrentDirectory, "GameData", TextureFolder));
			if (!TexFolder.Exists)
			{
				LogError("未找到贴图目录:" + TextureFolder);
				return;
			}
			ddsFiles = TexFolder.GetFiles("*.dds");
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
					Log("cfg给GameObject " + pair[0] + "指定了shader名称:" + pair[1]);
#endif
				}
			}

			//读取修改过的材质名称-贴图名称映射
			matTexMapping = new Dictionary<string, string>();
			if (!string.IsNullOrEmpty(MatTexMapping))
			{
#if DEBUG
				Log("正在读取Mat-Tex Mapping...");
#endif
				foreach (string s in MatTexMapping.Replace(" ", "").Split(';'))
				{
					string[] pair = s.Split(':');
					if (pair.Length != 2) continue;
					matTexMapping.Add(pair[0], pair[1]);
#if DEBUG
					Log("增加了材质名到贴图搜索关键词的映射修改:" + pair[0] + "-" + pair[1]);
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
					string[] sarray1 = s.Split(',');//[TransformName]:metallic:0.7	smoothness:0.3
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
			foreach (KeyValuePair<string, List<Renderer>> MatName_Renderers_Pair in materialDict)
			{
				//遍历Renderer列表
				foreach (Renderer renderer in MatName_Renderers_Pair.Value)
				{
					//替换shader
					if (TransformShaderPairs.ContainsKey(renderer.gameObject.name))
					{
#if DEBUG
						Log("正在给" + renderer.gameObject.name + "替换shader:" + TransformShaderPairs[renderer.gameObject.name]);
#endif
						switch (TransformShaderPairs[renderer.gameObject.name])
						{
							case "cutout": renderer.sharedMaterial.shader = CutoffShader; break;
							case "transparent": renderer.sharedMaterial.shader = TransparentShader; break;
							case "blackbody": renderer.sharedMaterial.shader = BlackBodyShader; break;
							case "lit": renderer.sharedMaterial.shader = LitShader; break;
							default:
								{
									LogError("未找到Shader\"" + TransformShaderPairs[renderer.gameObject.name] + "\"for GameObject \"" + renderer.gameObject.name + "\".正在使用Lit shader.\n" +
										"KerwisShader插件现版本除了默认Shader\"Lit\"外只有三种:\"cutout\",\"transparent\",\"blackbody\".");
									renderer.sharedMaterial.shader = LitShader; break;
								}
						}
					}
					else renderer.sharedMaterial.shader = LitShader;
					//如果cfg有专门指定当前Transform的材质参数
					if (TransformMatParamPairs.ContainsKey(renderer.gameObject.name))
					{
#if DEBUG
						Log("正在给" + renderer.gameObject.name + "设置参数...");
#endif
						foreach (KeyValuePair<MatParamType, float> param in TransformMatParamPairs[renderer.gameObject.name])
							switch (param.Key)
							{
								case MatParamType.metallic: renderer.sharedMaterial.SetFloat("_Metallic", param.Value); break;
								case MatParamType.smoothness: renderer.sharedMaterial.SetFloat("_Smoothness", param.Value); break;
								case MatParamType.ambient: renderer.sharedMaterial.SetFloat("_AmbientMultiplier", param.Value); break;
							}
					}
				}
			}
			if(string.IsNullOrWhiteSpace(CurrentTextureVariant))
            {
				Fields[nameof(CurrentTextureVariant)].guiActive = false;
				Fields[nameof(CurrentTextureVariant)].guiActiveEditor = false;
				CurrentTextureVariant = "";
			}
            else
			{
				UI_ChooseOption chooseTexUI = (UI_ChooseOption)Fields[CurrentTextureVariant].uiControlEditor;
				chooseTexUI.onFieldChanged = ChangeTexture;
				var TexVariantsArray = TextureVariantKeys.Replace(" ", "").Trim(';').Split(';');
				chooseTexUI.options = TexVariantsArray;
				CurrentTextureVariant = TexVariantsArray[0];
			}
			ChangeTexture(Fields[nameof(CurrentTextureVariant)], 0);
#if DEBUG
			Fields["metallic"].uiControlEditor.onFieldChanged = ChangeMetallic;
			Fields["metallic"].uiControlFlight.onFieldChanged = ChangeMetallic;
			Fields["smoothness"].uiControlEditor.onFieldChanged = ChangeSmoothness;
			Fields["smoothness"].uiControlFlight.onFieldChanged = ChangeSmoothness;
			Fields["ao"].uiControlEditor.onFieldChanged = ChangeAmbientOcclusion;
			Fields["ao"].uiControlFlight.onFieldChanged = ChangeAmbientOcclusion;
			BaseField baseField = Fields["EditingMat"];
			baseField.uiControlEditor.onFieldChanged = ChangeSelectedMat;
			baseField.uiControlFlight.onFieldChanged = ChangeSelectedMat;
			string[] array = materialDict.Keys.ToArray();
			((UI_ChooseOption)baseField.uiControlEditor).options = array;
			((UI_ChooseOption)baseField.uiControlFlight).options = array;
			EditingMat = array[0];
			ChangeSelectedMat(baseField, 1f);
#endif
		}
		void ChangeTexture(BaseField field, object oldValueObj)
        {
			Log(CurrentTextureVariant);
			foreach (KeyValuePair<string, List<Renderer>> MatName_Renderers_Pair in materialDict)
				foreach (FileInfo ddsInfo in ddsFiles)
                {
                    if (ddsInfo.Name.Contains(matTexMapping.ContainsKey(MatName_Renderers_Pair.Key) ? matTexMapping[MatName_Renderers_Pair.Key] : MatName_Renderers_Pair.Key) && ddsInfo.Name.Contains(CurrentTextureVariant))
							if (ddsInfo.Name.Contains("BaseMap"))
								foreach (Renderer r in MatName_Renderers_Pair.Value)
									r.sharedMaterial.SetTexture("_BaseMap", DDSLoader.Instance.FromFile(ddsInfo.FullName));
							else if (ddsInfo.Name.Contains("MaskMap"))
								foreach (Renderer r in MatName_Renderers_Pair.Value)
									r.sharedMaterial.SetTexture("_MaskMap", DDSLoader.Instance.FromFile(ddsInfo.FullName));
							else if (ddsInfo.Name.Contains("Normal"))
								foreach (Renderer r in MatName_Renderers_Pair.Value)
									r.sharedMaterial.SetTexture("_NormalMap", DDSLoader.Instance.FromFile(ddsInfo.FullName));
							else if (ddsInfo.Name.Contains("Emissive"))
								foreach (Renderer r in MatName_Renderers_Pair.Value)
									r.sharedMaterial.SetTexture("_EmissiveMap", DDSLoader.Instance.FromFile(ddsInfo.FullName));
				}
		}
#if DEBUG
		private void ChangeMetallic(BaseField field, object oldValueObj)
		{
			if (materialDict.TryGetValue(this.EditingMat, out List<Renderer> list))
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
            }
        }
#endif
		void Log(string message)
		{
			Debug.Log("KerwisShader@" + part.name + ":" + message);
		}
		void LogError(string message)
		{
			Debug.LogError("KerwisShader@" + part.name + ":" + message);
		}
	}
}