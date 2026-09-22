using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public partial class HandMusicController
{
    private static readonly string[] NoteNames={"—","C","D","E","F","G","A"};
    private GameObject songSelector;
    private bool selectingSong;
    private static string NoteInstruction(int note) => note==6?"Pinch thumb + index":$"{note} finger{(note==1?"":"s")}";
    private RectTransform cameraBox,statusBox,guidePanel,compactPanel;
    private HandTrackingOverlay overlay;
    private HandPoseGraphic nextPose;
    private TMP_Text roleLabel,detectionLabel,nextLabel,songLabel,compactLabel,fullButton;
    private bool fullCamera;
    private readonly FingerMaskFilter[] fingerFilters={new FingerMaskFilter(),new FingerMaskFilter()};
    private string detectionSummary="Show both hands";

    private void ProcessHands(bool detected,float elapsed)
    {
        int control=-1,note=-1;
        float controlConfidence=0,noteConfidence=0;
        ClearOverlay();
        if(detected && result.handedness!=null && result.handWorldLandmarks!=null && result.handLandmarks!=null)
        {
            int count=Mathf.Min(result.handedness.Count,Mathf.Min(result.handWorldLandmarks.Count,result.handLandmarks.Count));
            for(int i=0;i<count;i++)
            {
                var categories=result.handedness[i].categories;
                if(categories==null || categories.Count==0 || result.handWorldLandmarks[i].landmarks.Count<21 || result.handLandmarks[i].landmarks.Count<21)continue;
                var category=categories[0];
                if(category.score<.7f)continue;
                bool isLeft=category.categoryName=="Left";
                if(category.categoryName!="Left" && category.categoryName!="Right")continue;
                // The input is a mirrored selfie. For an unmirrored external camera, swap the label.
                if(!front)isLeft=!isLeft;
                if(isLeft==controlLeft)
                { if(category.score>controlConfidence) { control=i;controlConfidence=category.score; } }
                else if(category.score>noteConfidence) { note=i;noteConfidence=category.score; }
            }
        }
        int controlCount=-1,noteCount=-1;
        MusicGesture controlPose=MusicGesture.None;
        for(int role=0;role<2;role++)
        {
            int index=role==0?control:note;
            if(index<0) { fingerFilters[role].Reset();continue; }
            var world=result.handWorldLandmarks[index].landmarks;
            var normalized=result.handLandmarks[index].landmarks;
            for(int j=0;j<21;j++)
            {
                landmarks[j]=new Vector3(world[j].x,world[j].y,world[j].z);
                overlay.points[role][j]=new Vector2(normalized[j].x,1-normalized[j].y);
            }
            overlay.visible[role]=true;
            overlay.masks[role]=fingerFilters[role].Sample(HandGesture.FingerMask(landmarks));
            int raised=HandGesture.FingerCount(overlay.masks[role]);
            if(role==0) { controlCount=raised;controlPose=HandGesture.Classify(landmarks); }
            else noteCount=HandGesture.Classify(landmarks)==MusicGesture.Pinch?6:raised;
        }
        overlay.SetVerticesDirty();
        if(selectingSong) { StopPlaying();return; }
        if(control<0 || controlPose==MusicGesture.Fist) StopPlaying();
        else if(controlPose==MusicGesture.Open)
        {
            openHeld+=elapsed;
            if(openHeld>=.25f)playing=true;
        }
        else openHeld=0;
        // Both hands are needed; a missing note hand also silences a ringing note.
        if(note<0)audioSource.Stop();
        bool fired=gate.Sample(Mathf.Max(0,noteCount),note>=0,playing,elapsed,holdSeconds);
        bool correct=false;
        if(fired)
        {
            audioSource.Stop();audioSource.PlayOneShot(notes[noteCount-1],.65f);
            notesPlayed++;countLabel.text="Notes: "+notesPlayed;
            correct=song.Play(noteCount);
        }
        string controlStatus=control<0?"not visible":playing?"PLAY":"STOP";
        detectionSummary=$"Control: {controlStatus} · Note hand: {(noteCount<0?"not visible":NoteInstruction(noteCount))}";
        detectionLabel.text=$"Control: {controlStatus}   ({(controlCount<0?"—":controlCount.ToString())})\nNotes: {(noteCount<0?"not visible":NoteInstruction(noteCount)+" = "+NoteNames[noteCount])}\nWhite fingertips = raised";
        progress.fillAmount=gate.Progress(holdSeconds);
        if(control<0)status.text="Show your control hand. Playback is stopped.";
        else if(!playing)status.text="Open your control hand to enable playing.";
        else if(note<0)status.text="Playing enabled. Show your note hand.";
        else if(song.Complete)status.text="Song complete! Restart the song to try again.";
        else if(fired && !correct)status.text=$"You played {NoteNames[noteCount]}. Try {NoteNames[song.Next]}: {NoteInstruction(song.Next)}.";
        else status.text="Hold the note gesture. Lower fingers to repeat the same note.";
        RefreshSongGuide();
    }

    private void StopPlaying()
    {
        playing=false;openHeld=0;
        if(audioSource!=null)audioSource.Stop();
    }
    private void ClearOverlay()
    {
        if(overlay==null)return;
        overlay.visible[0]=overlay.visible[1]=false;overlay.SetVerticesDirty();
    }
    private void SwapHands()
    {
        foreach(var filter in fingerFilters)filter.Reset();
        controlLeft=!controlLeft;StopPlaying();gate.Reset();ClearOverlay();RefreshRoles();
        status.text="Hands swapped. Open your new control hand.";
    }
    private void RestartSong()
    {
        song.Reset();gate.Reset();StopPlaying();notesPlayed=0;countLabel.text="Notes: 0";RefreshSongGuide();
        status.text="Song restarted. Open your control hand to play.";
    }
    private void RefreshRoles()
    {
        roleLabel.text=$"{(controlLeft?"Left":"Right")} hand: open = play, fist = stop\n{(controlLeft?"Right":"Left")} hand: count fingers for notes";
    }
    private void RefreshSongGuide()
    {
        songLabel.text=$"{song.Title}\n{song.Position}/{song.Length} notes";
        nextLabel.text=song.Complete?"Well played!":$"Next: {NoteNames[song.Next]}\n{NoteInstruction(song.Next)}";
        nextPose.gesture=song.Next==6?MusicGesture.Pinch:MusicGesture.Open;
        nextPose.fingerCount=song.Next==6?-1:song.Next;nextPose.SetVerticesDirty();
        compactLabel.text=(song.Complete?"Song complete!":$"Next: {NoteNames[song.Next]} · {NoteInstruction(song.Next)}   |   {song.Position}/{song.Length}")+"\n"+detectionSummary;
    }

    private void BuildGuideUI()
    {
        guidePanel=Rect("Song guidance",safe,new Vector2(.68f,.02f),new Vector2(.98f,.85f));
        ModernUI.Surface(guidePanel.gameObject.AddComponent<Image>(),Color.white,true);
        songLabel=Text(guidePanel,"",new Vector2(.04f,.86f),new Vector2(.96f,.99f),30);
        nextPose=Rect("Next fingers",guidePanel,new Vector2(.03f,.64f),new Vector2(.31f,.87f)).gameObject.AddComponent<HandPoseGraphic>();
        nextPose.gesture=MusicGesture.Open;nextPose.color=ClinicalMenu.Teal;nextPose.raycastTarget=false;
        nextLabel=Text(guidePanel,"",new Vector2(.36f,.64f),new Vector2(.96f,.85f),38);
        roleLabel=Text(guidePanel,"",new Vector2(.04f,.49f),new Vector2(.96f,.64f),29);
        detectionLabel=Text(guidePanel,"Control: not visible\nNotes: not visible\nWhite fingertips = raised",new Vector2(.04f,.30f),new Vector2(.96f,.48f),30);
        Text(guidePanel,"1=C  2=D  3=E  4=F  5=G  Pinch=A",new Vector2(.04f,.23f),new Vector2(.96f,.30f),26);
        Button(guidePanel,"Swap hands",new Vector2(.04f,.145f),new Vector2(.48f,.225f),SwapHands);
        Button(guidePanel,"Restart song",new Vector2(.52f,.145f),new Vector2(.96f,.225f),RestartSong);
        holdLabel=Text(guidePanel,"Hold time: 0.6s",new Vector2(.04f,.075f),new Vector2(.96f,.14f),30);
        Button(guidePanel,"Slower",new Vector2(.04f,.01f),new Vector2(.48f,.075f),()=>ChangeHold(.2f));
        Button(guidePanel,"Faster",new Vector2(.52f,.01f),new Vector2(.96f,.075f),()=>ChangeHold(-.2f));
        compactPanel=Rect("Full camera guidance",safe,new Vector2(.30f,.70f),new Vector2(.97f,.86f));
        ModernUI.Surface(compactPanel.gameObject.AddComponent<Image>(),ClinicalMenu.Paper);
        compactLabel=Text(compactPanel,"",new Vector2(.02f,.04f),new Vector2(.98f,.96f),38);
        compactPanel.gameObject.SetActive(false);
        RefreshRoles();RefreshSongGuide();
    }
    private void BuildTopBar()
    {
        var bar=Rect("Toolbar",safe,new Vector2(.02f,.88f),new Vector2(.98f,.98f));
        ModernUI.Surface(bar.gameObject.AddComponent<Image>(),ClinicalMenu.Paper);
        Text(bar,"Hand Music",new Vector2(.01f,0),new Vector2(.21f,1),42);
        countLabel=Text(bar,"Notes: 0",new Vector2(.215f,0),new Vector2(.33f,1),30);
        Button(bar,"Choose song",new Vector2(.34f,.04f),new Vector2(.55f,.96f),OpenSongSelector);
        fullButton=Button(bar,"Full camera",new Vector2(.565f,.04f),new Vector2(.77f,.96f),ToggleFullCamera);
        Button(bar,"Back to menu",new Vector2(.785f,.04f),new Vector2(.99f,.96f),()=>SceneManager.LoadScene("MenuScene"));
    }
    private void BuildSongSelector()
    {
        var panel=Rect("Choose a song",safe,Vector2.zero,Vector2.one);
        panel.gameObject.AddComponent<Image>().color=ClinicalMenu.Paper;
        songSelector=panel.gameObject;
        Text(panel,"Choose a song",new Vector2(.12f,.78f),new Vector2(.9f,.94f),60);
        Text(panel,"Follow the next note and gesture. The song waits for you.",new Vector2(.12f,.66f),new Vector2(.9f,.77f),36);
        for(int i=0;i<GuidedHandSong.Titles.Length;i++)
        {
            int choice=i;float top=.60f-i*.16f;
            Button(panel,GuidedHandSong.Titles[i],new Vector2(.12f,top-.12f),new Vector2(.88f,top),()=>SelectSong(choice));
        }
        Text(panel,"Twinkle adds A: pinch thumb and index together, with another finger raised.",new Vector2(.12f,.15f),new Vector2(.9f,.29f),34);
        Button(panel,"Cancel",new Vector2(.36f,.03f),new Vector2(.64f,.13f),()=>{ selectingSong=false;songSelector.SetActive(false); });
        songSelector.SetActive(false);
    }
    private void OpenSongSelector()
    {
        StopPlaying();gate.Reset();selectingSong=true;songSelector.SetActive(true);
    }
    private void SelectSong(int index)
    {
        song.Select(index);selectingSong=false;songSelector.SetActive(false);RestartSong();
    }
    private void ToggleFullCamera()
    {
        fullCamera=!fullCamera;
        cameraBox.anchorMin=fullCamera?Vector2.zero:new Vector2(.02f,.22f);
        cameraBox.anchorMax=fullCamera?Vector2.one:new Vector2(.65f,.85f);
        cameraBox.offsetMin=cameraBox.offsetMax=Vector2.zero;
        guidePanel.gameObject.SetActive(!fullCamera);compactPanel.gameObject.SetActive(fullCamera);
        statusBox.anchorMax=new Vector2(fullCamera?.98f:.65f,fullCamera?.15f:.19f);
        fullButton.text=fullCamera?"Show controls":"Full camera";
    }
}
