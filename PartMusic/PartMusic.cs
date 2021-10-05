using UnityEngine;

namespace PartMusic
{
    public class PartMusic : PartModule
    {
        [KSPField]
        public string AudioFile = "";

        private AudioSource player;

        [UI_Toggle(scene = UI_Scene.All,enabledText = "#autoLOC_PartMusic_Playing", disabledText = "#autoLOC_PartMusic_Stopped")]
        [KSPField(guiName = "#autoLOC_PartMusic", isPersistant = true,guiActive =true)]
        public bool isPlayingMusic;

        void Start()
        {
            player = part.gameObject.AddComponent<AudioSource>();
            player.clip = GameDatabase.Instance.GetAudioClip(AudioFile);
            player.loop = true;
        }

        void Update()
        {
            if (isPlayingMusic && !player.isPlaying)
            {
                player.Play();
            }
            if (!isPlayingMusic && player.isPlaying)
            {
                player.Stop();
            }
        }
    }
}
