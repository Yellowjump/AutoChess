//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using DataTable;
using GameFramework;
using GameFramework.DataTable;
using GameFramework.Sound;
using UnityEngine;

namespace UnityGameFramework.Runtime
{
    public static class SoundExtension
    {
        private const float FadeVolumeDuration = 1f;
        private static int? s_MusicSerialId = null;

        public static int? PlayMusic(this SoundComponent soundComponent, int musicId, object userData = null)
        {
            soundComponent.StopMusic();

            IDataTable<DRSound> dtMusicAsset = GameEntry.DataTable.GetDataTable<DRSound>("Sound");
            DRSound drMusic = dtMusicAsset.GetDataRow(musicId);
            if (drMusic == null)
            {
                Log.Warning("Can not load music '{0}' from sound table.", musicId.ToString());
                return null;
            }

            var musicAssetPath = AssetUtility.GetAssetPathByID(drMusic.AssetId);
            if (string.IsNullOrEmpty(musicAssetPath))
            {
                Log.Warning("Can not load music '{0}' from asset table.", drMusic.AssetId.ToString());
                return null;
            }
            PlaySoundParams playSoundParams = PlaySoundParams.Create();
            playSoundParams.Priority = 64;
            playSoundParams.Loop = true;
            playSoundParams.VolumeInSoundGroup = 1f;
            playSoundParams.VolumeMaster = soundComponent.MasterVolume;
            playSoundParams.FadeInSeconds = FadeVolumeDuration;
            playSoundParams.SpatialBlend = 0f;
            s_MusicSerialId = soundComponent.PlaySound(musicAssetPath, "BGM", ConstValue.AssetPriority.MusicAsset, playSoundParams, null, userData);
            return s_MusicSerialId;
        }

        public static void StopMusic(this SoundComponent soundComponent)
        {
            if (!s_MusicSerialId.HasValue)
            {
                return;
            }

            soundComponent.StopSound(s_MusicSerialId.Value, FadeVolumeDuration);
            s_MusicSerialId = null;
        }

        public static int? PlaySfxSound(this SoundComponent soundComponent, int soundId, Vector3 worldPos, object userData = null)
        {
            IDataTable<DRSound> dtSound = GameEntry.DataTable.GetDataTable<DRSound>("Sound");
            DRSound drSound = dtSound.GetDataRow(soundId);
            if (drSound == null)
            {
                Log.Warning("Can not load sound '{0}' from data table.", soundId.ToString());
                return null;
            }

            PlaySoundParams playSoundParams = PlaySoundParams.Create();
            playSoundParams.Priority = drSound.Priority;
            playSoundParams.Loop = drSound.Loop;
            playSoundParams.VolumeInSoundGroup = drSound.Volume;
            playSoundParams.VolumeMaster = soundComponent.MasterVolume;
            playSoundParams.Pitch = (float)Utility.Random.GetRandomNoLogic(0.95d, 1.05d);
            //playSoundParams.SpatialBlend = drSound.SpatialBlend;
            return soundComponent.PlaySound(AssetUtility.GetAssetPathByID(drSound.AssetId), drSound.SoundGroupId, ConstValue.AssetPriority.SoundAsset, playSoundParams, worldPos, userData);
        }

        public static int? PlayUISound(this SoundComponent soundComponent, int uiSoundId, object userData = null)
        {
            IDataTable<DRSound> dtUISound = GameEntry.DataTable.GetDataTable<DRSound>("Sound");
            DRSound drUISound = dtUISound.GetDataRow(uiSoundId);
            if (drUISound == null)
            {
                Log.Warning("Can not load UI sound '{0}' from data table.", uiSoundId.ToString());
                return null;
            }

            PlaySoundParams playSoundParams = PlaySoundParams.Create();
            playSoundParams.Priority = drUISound.Priority;
            playSoundParams.Loop = false;
            playSoundParams.VolumeInSoundGroup = drUISound.Volume;
            playSoundParams.VolumeMaster = soundComponent.MasterVolume;
            playSoundParams.SpatialBlend = 0f;
            return soundComponent.PlaySound(AssetUtility.GetAssetPathByID(drUISound.AssetId), drUISound.SoundGroupId, ConstValue.AssetPriority.UISoundAsset, playSoundParams, userData);
        }

        public static bool IsMuted(this SoundComponent soundComponent, string soundGroupName)
        {
            if (string.IsNullOrEmpty(soundGroupName))
            {
                Log.Warning("Sound group is invalid.");
                return true;
            }

            ISoundGroup soundGroup = soundComponent.GetSoundGroup(soundGroupName);
            if (soundGroup == null)
            {
                Log.Warning("Sound group '{0}' is invalid.", soundGroupName);
                return true;
            }

            return soundGroup.Mute;
        }

        public static void Mute(this SoundComponent soundComponent, string soundGroupName, bool mute)
        {
            if (string.IsNullOrEmpty(soundGroupName))
            {
                Log.Warning("Sound group is invalid.");
                return;
            }

            ISoundGroup soundGroup = soundComponent.GetSoundGroup(soundGroupName);
            if (soundGroup == null)
            {
                Log.Warning("Sound group '{0}' is invalid.", soundGroupName);
                return;
            }

            soundGroup.Mute = mute;

            //GameEntry.Setting.SetBool(Utility.Text.Format(Constant.Setting.SoundGroupMuted, soundGroupName), mute);
            GameEntry.Setting.Save();
        }

        public static float GetVolume(this SoundComponent soundComponent, string soundGroupName)
        {
            if (string.IsNullOrEmpty(soundGroupName))
            {
                Log.Warning("Sound group is invalid.");
                return 0f;
            }

            ISoundGroup soundGroup = soundComponent.GetSoundGroup(soundGroupName);
            if (soundGroup == null)
            {
                Log.Warning("Sound group '{0}' is invalid.", soundGroupName);
                return 0f;
            }

            return soundGroup.Volume;
        }

        public static void SetVolume(this SoundComponent soundComponent, string soundGroupName, float volume)
        {
            if (string.IsNullOrEmpty(soundGroupName))
            {
                Log.Warning("Sound group is invalid.");
                return;
            }

            ISoundGroup soundGroup = soundComponent.GetSoundGroup(soundGroupName);
            if (soundGroup == null)
            {
                Log.Warning("Sound group '{0}' is invalid.", soundGroupName);
                return;
            }

            soundGroup.Volume = volume;
            GameEntry.Setting.SetFloat(Utility.Text.Format(ConstValue.SettingKeySoundGroupVolume, soundGroupName), volume);
            GameEntry.Setting.Save();
        }

        public static void SetMasterVolume(this SoundComponent soundComponent,float volume)
        {
            soundComponent.MasterVolume = volume;
            var allSoundGroup = soundComponent.GetAllSoundGroups();
            foreach (var oneSoundGroup in allSoundGroup)
            {
                oneSoundGroup.SetMasterVolume(volume);
            }
            GameEntry.Setting.SetFloat(Utility.Text.Format(ConstValue.SettingKeySoundGroupVolume, "Master"), volume);
            GameEntry.Setting.Save();
        }
        public static float GetMasterVolume(this SoundComponent soundComponent)
        {
            return soundComponent.MasterVolume;
        }
    }
}
