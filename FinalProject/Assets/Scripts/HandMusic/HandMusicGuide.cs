using UnityEngine;
using UnityEngine.UI;
using TMPro;

public partial class HandMusicController
{
    private static readonly string[] NoteNames={"-","C","D","E","F","G","A"};
    private GameObject songSelector, measureAgain, circleRow;
    private bool selectingSong;
    private RectTransform cameraBox,guidePanel,compactPanel;
    private HandTrackingOverlay overlay;
    private HandPoseGraphic nextPose;
    private TMP_Text detectionLabel,nextLabel,actionLabel,songLabel,compactLabel,fullButton,circleLabel;
    private bool fullCamera;

    private void StopPlaying()
    {
        if(audioSource!=null)audioSource.Stop();
    }
    private void ClearOverlay()
    {
        if(overlay==null)return;
        overlay.visible[0]=overlay.visible[1]=false;overlay.SetVerticesDirty();
    }
    private void RestartSong()
    {
        song.Reset();dwell.Reset();StopPlaying();
        if(sessionRunning && CircleMode)NextTarget();
        RefreshSongGuide();
        status.text="Song restarted from the beginning.";
    }
    private void MeasureAgain()
    {
        calibration.Reset();openGate.Reset();progress.fillAmount=0;
        status.text=$"Let's measure again. Show your {Side} hand.";
    }
    // How to play the next note: the finger count (or pinch) in Fingers mode, an open hand otherwise.
    private string NextGesture() => Mode==HandMusicMode.OpenHand ? "Open hand" : song.Next==6 ? "Pinch" : Capital(Shown(song.Next));
    private string NextAction() => Mode==HandMusicMode.OpenHand ? $"Open your {Side} hand"
        : song.Next==6 ? "Pinch thumb and index, another finger up, in the gold circle" : "Hold them up in the gold circle";
    private void RefreshSongGuide()
    {
        songLabel.text=$"{song.Title}\n{song.Position}/{song.Length} notes";
        nextLabel.text=song.Complete?"Well played!":$"Next: {NoteNames[song.Next]}\n<size=34>{NextGesture()}</size>";
        actionLabel.text=song.Complete?"The song starts again in a moment.":NextAction();
        // The picture shows the trained hand as it appears in the mirrored camera view.
        nextPose.mirrored=TrainLeft;
        nextPose.gesture=Mode==HandMusicMode.Fingers && song.Next==6?MusicGesture.Pinch:MusicGesture.Open;
        nextPose.fingerCount=Mode==HandMusicMode.Fingers && song.Next!=6?song.Next:-1;nextPose.SetVerticesDirty();
        detectionLabel.text=detectionSummary+(Mode==HandMusicMode.Fingers?" · white tips = raised":"");
        compactLabel.text=(song.Complete?"Song complete!":$"Next: {NoteNames[song.Next]} · {NextGesture()} · {NextAction()}   |   {song.Position}/{song.Length}")+"\n"+status.text;
    }

    private void BuildGuideUI()
    {
        guidePanel=Panel("Song guidance",safe,new Vector2(.66f,.03f),new Vector2(.98f,.865f));
        ModernUI.Surface(guidePanel.gameObject.AddComponent<Image>(),Color.white,true);
        status=Text(guidePanel,"Starting front camera…",new Vector2(.05f,.855f),new Vector2(.95f,.975f),30);
        nextPose=Rect("Next hand pose",guidePanel,new Vector2(.04f,.61f),new Vector2(.36f,.85f)).gameObject.AddComponent<HandPoseGraphic>();
        nextPose.gesture=MusicGesture.Open;nextPose.color=ClinicalMenu.Teal;nextPose.raycastTarget=false;
        nextLabel=Text(guidePanel,"",new Vector2(.4f,.61f),new Vector2(.95f,.85f),48);
        actionLabel=Text(guidePanel,"",new Vector2(.05f,.52f),new Vector2(.95f,.61f),28);
        songLabel=Text(guidePanel,"",new Vector2(.05f,.44f),new Vector2(.95f,.52f),26);
        var track=Rect("Hold progress",guidePanel,new Vector2(.05f,.41f),new Vector2(.95f,.43f));
        track.gameObject.AddComponent<Image>().color=new Color(.77f,.87f,.88f);
        progress=Rect("Progress",track,Vector2.zero,Vector2.one).gameObject.AddComponent<Image>();
        // Filled images need a sprite; a plain one keeps the bar square like its track.
        progress.sprite=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,4,4),new Vector2(.5f,.5f));
        progress.color=ClinicalMenu.Teal; progress.type=Image.Type.Filled; progress.fillMethod=Image.FillMethod.Horizontal; progress.fillAmount=0;
        detectionLabel=Text(guidePanel,"",new Vector2(.05f,.31f),new Vector2(.95f,.4f),26);
        Button(guidePanel,"Restart song",new Vector2(.05f,.215f),new Vector2(CircleMode?.95f:.48f,.295f),RestartSong);
        measureAgain=Button(guidePanel,"Measure again",new Vector2(.52f,.215f),new Vector2(.95f,.295f),MeasureAgain).transform.parent.gameObject;
        measureAgain.SetActive(!CircleMode);
        // The gold circle can be resized during play; the size is saved like the other settings.
        circleRow=Rect("Circle size",guidePanel,new Vector2(.05f,.12f),new Vector2(.95f,.2f)).gameObject;
        circleLabel=Text(circleRow.transform,$"Circle: {GameSettings.handMusicCircle:P0}",new Vector2(0,0),new Vector2(.31f,1),28);
        Button(circleRow.transform,"Smaller",new Vector2(.33f,0),new Vector2(.65f,1),()=>ChangeCircle(-.1f));
        Button(circleRow.transform,"Bigger",new Vector2(.67f,0),new Vector2(1,1),()=>ChangeCircle(.1f));
        circleRow.SetActive(CircleMode);
        holdLabel=Text(guidePanel,$"Hold: {GameSettings.handMusicHold:0.0}s",new Vector2(.05f,.02f),new Vector2(.33f,.1f),28);
        Button(guidePanel,"Slower",new Vector2(.35f,.02f),new Vector2(.64f,.1f),()=>ChangeHold(.1f));
        Button(guidePanel,"Faster",new Vector2(.66f,.02f),new Vector2(.95f,.1f),()=>ChangeHold(-.1f));
        // Full camera guidance stays on the other half, clear of the trained side's circles and hand.
        compactPanel=Panel("Full camera guidance",safe,new Vector2(.5f,.68f),new Vector2(.97f,.86f));
        ModernUI.Surface(compactPanel.gameObject.AddComponent<Image>(),ClinicalMenu.Paper);
        compactLabel=Text(compactPanel,"",new Vector2(.02f,.04f),new Vector2(.98f,.96f),32);
        compactPanel.gameObject.SetActive(false);
        detectionSummary=$"{Capital(Side)} hand: not visible\n{Capital(GameSettings.OtherSide)} hand: resting";
        RefreshSongGuide();
    }
    private void BuildTopBar()
    {
        var bar=Rect("Toolbar",safe,new Vector2(.02f,.885f),new Vector2(.98f,.985f));
        ModernUI.Surface(bar.gameObject.AddComponent<Image>(),ClinicalMenu.Paper);
        // Mirrored with the layout: the score, time and End session sit on the side that is not neglected.
        Mirror(Text(bar,"Hand Music",new Vector2(.01f,0),new Vector2(.15f,1),40).rectTransform);
        Mirror(Button(bar,"Choose song",new Vector2(.16f,.06f),new Vector2(.31f,.94f),OpenSongSelector).transform.parent);
        fullButton=Button(bar,"Full camera",new Vector2(.32f,.06f),new Vector2(.48f,.94f),ToggleFullCamera);Mirror(fullButton.transform.parent);
        countLabel=Text(bar,"Notes: 0",new Vector2(.51f,0),new Vector2(.64f,1),30);Mirror(countLabel.rectTransform);
        timeLabel=Text(bar,"Time",new Vector2(.65f,0),new Vector2(.78f,1),30);Mirror(timeLabel.rectTransform);
        Mirror(Button(bar,"End session",new Vector2(.79f,.06f),new Vector2(.99f,.94f),()=>FinishSession(false)).transform.parent);
    }
    private static void Mirror(Transform item) { var r=(RectTransform)item; Place(r,r.anchorMin,r.anchorMax); }
    // Centred, so it reads the same whichever side is neglected.
    private void BuildSongSelector()
    {
        var panel=Rect("Choose a song",safe,Vector2.zero,Vector2.one);
        panel.gameObject.AddComponent<Image>().color=ClinicalMenu.Paper;
        songSelector=panel.gameObject;
        Text(panel,"Choose a song",new Vector2(.12f,.78f),new Vector2(.88f,.94f),60).alignment=TextAlignmentOptions.Center;
        Text(panel,"The song waits for you and repeats until the time is up.",new Vector2(.12f,.66f),new Vector2(.88f,.77f),36).alignment=TextAlignmentOptions.Center;
        for(int i=0;i<GuidedHandSong.Titles.Length;i++)
        {
            int choice=i;float top=.60f-i*.16f;
            Button(panel,GuidedHandSong.Titles[i],new Vector2(.12f,top-.12f),new Vector2(.88f,top),()=>SelectSong(choice));
        }
        Text(panel,"In Fingers mode, Twinkle adds A: pinch thumb and index together, with another finger raised.",new Vector2(.12f,.15f),new Vector2(.88f,.29f),34).alignment=TextAlignmentOptions.Center;
        Button(panel,"Cancel",new Vector2(.36f,.03f),new Vector2(.64f,.13f),()=>{ selectingSong=false;songSelector.SetActive(false); });
        songSelector.SetActive(false);
    }
    private void OpenSongSelector()
    {
        StopPlaying();dwell.Reset();selectingSong=true;songSelector.SetActive(true);
    }
    private void SelectSong(int index)
    {
        song.Select(index);selectingSong=false;songSelector.SetActive(false);RestartSong();
    }
    private void ToggleFullCamera()
    {
        fullCamera=!fullCamera;
        Place(cameraBox,fullCamera?Vector2.zero:new Vector2(.02f,.03f),fullCamera?Vector2.one:new Vector2(.645f,.865f));
        guidePanel.gameObject.SetActive(!fullCamera);compactPanel.gameObject.SetActive(fullCamera);
        fullButton.text=fullCamera?"Show controls":"Full camera";
    }
}
