﻿using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class BocceGame : MonoBehaviour
{
    enum GameMode
    {
        Pause = 0,
        Setup = 1,
        Aiming = 2,
        AimingFinish = 3,
        RoundResult = 4,
        GameResult = 5,
    }
    static GameMode gameMode;
    static GameMode prevGameMode;

    // cameras
    public Camera MainCamera;
    public Camera PlayerCamera;
    
    // game variables
    const int NumTeams = 2;
    const float MaxBallForce = 1000f;
    public Rigidbody BocceBallPrefab;
    
    Rigidbody jack = null;
    Rigidbody currentBall = null;
    
    // GUI
    bool forceIncreasing = true;    
    public GUIProgressBar BallForceBar;
    public float ForceIncrement = 0.01f;
    
    int[] teamScore = new int[NumTeams];
    public GUIText[] TeamScoreText = new GUIText[NumTeams];
    public GUIText MessageText;
    public GUIText HintMessageText;
    
    float currentWaitTime = 0.0f;
    const float minWaitBetweenThrows = 1.0f; // seconds
    
    public int BallsPerTeam = 4;
    public int WinningScore = 7;
    
    int currentTeam; // which team's turn it is
    int[] teamBalls = new int[NumTeams];
    List<float>[] teamBallDistanceSq = new List<float>[NumTeams];
    
    // menu
    public static bool Paused { get { return gameMode == GameMode.Pause; } }

    void Start()
    {
        for (int i = 0; i < NumTeams; ++i)
        {
            teamBallDistanceSq [i] = new List<float>();
            SetTeamScore(i, 0);
        }
    
        SetGameMode(GameMode.Setup);
    }
    
    #region Pause
    
    void TogglePause()
    {
        Pause(!Paused);
    }
    
    void Pause()
    {
        Pause(true);
    }
    
    void Pause(bool pause)
    {
        if (gameMode == GameMode.Pause)
        {
            gameMode = prevGameMode;
            
            Time.timeScale = 1.0f;
        } else
        {
            prevGameMode = gameMode;
            gameMode = GameMode.Pause;
            
            Time.timeScale = 0.0f;
        }

        if (Paused)
            Cursor.lockState = CursorLockMode.None;
        else
            Cursor.lockState = (gameMode == GameMode.Aiming) ? CursorLockMode.Locked : CursorLockMode.None;
    }
    
    #endregion
	
    void Update()
    {
        // pausing
        if (Input.GetKeyDown(KeyCode.Escape))
            TogglePause();
        
        if (!Paused)
        {
            switch (gameMode)
            {
                case GameMode.Setup:
                    {
                        currentBall = jack = CreateBall();
                        currentBall.transform.localScale *= 0.5f; // jack is smaller
                    
                        // random direction and force for the JACK
                        Quaternion xQuaternion = Quaternion.AngleAxis(Random.Range(-30, 30), Vector3.up);
                        Quaternion yQuaternion = Quaternion.AngleAxis(Random.Range(0, 15), -Vector3.right);
                    
                        Quaternion originalRotation = transform.localRotation;
                        PlayerCamera.gameObject.transform.localRotation = originalRotation * xQuaternion * yQuaternion;
                        
                        BallForceBar.BarProgress = Random.Range(0.5f, 1.0f);
                    
                        ThrowBall(PlayerCamera.gameObject.transform.forward * (MaxBallForce * BallForceBar.BarProgress));
                    
                        SetGameMode(GameMode.AimingFinish);
                        break;
                    }
                case GameMode.Aiming:
                    {
                        if (Input.GetKeyDown(KeyCode.Space))
                        {                        
                            currentBall = CreateBall(currentTeam);
                            teamBalls [currentTeam] -= 1;
                        
                            ThrowBall(PlayerCamera.gameObject.transform.forward * (MaxBallForce * BallForceBar.BarProgress));
                        
                            SetGameMode(GameMode.AimingFinish);
                        } else
                        {
                            if (forceIncreasing)
                            {
                                BallForceBar.BarProgress += ForceIncrement;
                                if (BallForceBar.BarProgress >= 1.0f)
                                {
                                    BallForceBar.BarProgress = 1.0f;
                                    forceIncreasing = false;
                                }
                            } else
                            {
                                BallForceBar.BarProgress -= ForceIncrement;
                                if (BallForceBar.BarProgress <= 0.0f)
                                {
                                    BallForceBar.BarProgress = 0.0f;
                                    forceIncreasing = true;
                                }
                            }
                        }
                    
                        break;
                    }
                case GameMode.AimingFinish:
                    {
                        if (currentWaitTime <= 0.0f)
                        {
                            if (!AreBallsMoving())
                            {
                                if (prevGameMode != GameMode.Setup)
                                {
                                    int closestTeam = GetClosestTeam();
                                    currentTeam = (closestTeam == 0 ? 1 : 0);
                                    // if the team is out of balls let the other finish
                                    if (teamBalls [currentTeam] == 0)
                                        currentTeam = (currentTeam == 0 ? 1 : 0);
                                }
                                
                                BocceBall jackComponent = jack.GetComponent<BocceBall>();
                                if (jackComponent && !jackComponent.InBounds)
                                {
                                    // jack is out of bounds
                                    SetGameMode(GameMode.Setup);
                                } else
                                    SetGameMode(teamBalls.Sum() > 0 ? GameMode.Aiming : GameMode.RoundResult);
                            }
                        } else
                            currentWaitTime -= Time.deltaTime;
                        break;
                    }
                case GameMode.RoundResult:
                    {
                        if (Input.GetKeyDown(KeyCode.Space))
                        {
                            if (teamScore.Max() >= WinningScore)
                                SetGameMode(GameMode.GameResult);
                            else
                                SetGameMode(GameMode.Setup);
                        }
                        break;
                    }                    
                case GameMode.GameResult:
                    {
                        if (Input.GetKeyDown(KeyCode.Space))
                            SceneManager.LoadScene(0);
                        break;
                    }
            }
        }
    }
    
    #region Events
    
    void OnApplicationFocus(bool focusStatus)
    {
        if (!Paused && !focusStatus)
            Pause();
    }
    
    #endregion
    
    bool AreBallsMoving()
    {
        IEnumerable<GameObject> gameObjects = GameObject.FindGameObjectsWithTag("BocceBall");
        bool inMotion = gameObjects.Count(ball => {
            BocceBall component = ball.GetComponent<BocceBall>();
            if (component && component.InBounds && component.IsMoving)
                return true;
            return false;
        }) > 0;
        
        return inMotion;
    }
    
    List<float> GetBallDistances(int team)
    {
        List<float> distances = new List<float>();
    
        IEnumerable<GameObject> gameObjects = GameObject.FindGameObjectsWithTag("BocceBall").Where(gameObject => {
            BocceBall component = gameObject.GetComponent<BocceBall>();
            return (component && component.Team == team && component.InBounds);
        });
        
        foreach (GameObject gameObject in gameObjects)
        {
            BocceBall component = gameObject.GetComponent<BocceBall>();
            if (component)
            {
                float distanceSq = component.GetDistanceSqToPoint(jack.transform.position);
                distances.Add(distanceSq);
            }
        }
        
        return distances;
    }
    
    int GetClosestTeam()
    {
        int team = currentTeam;
        
        float nearestDistanceSq = Mathf.Infinity;
        for (int i = 0; i < NumTeams; ++i)
        {
            List<float> distances = GetBallDistances(i);
            if (distances.Count() == 0)
                continue;
            
            float distanceSq = distances.Min();
            if (distanceSq < nearestDistanceSq)
            {
                nearestDistanceSq = distanceSq;
                team = i;
            }
        }
        
        return team;
    }
    
    Color GetTeamColor(int team)
    {
        if (team < 0 || team >= NumTeams)
            return Color.white;
        return TeamScoreText [team].GetComponent<GUIText>().color;
    }
    
    void SetTeamScore(int team, int score)
    {
        teamScore [team] = score;
        TeamScore component = TeamScoreText [team].GetComponent<TeamScore>();
        if (component)
            component.SetScore(score);
    }
    
    void SetGameMode(GameMode gameMode_)
    {
        Cursor.lockState = CursorLockMode.None;
        
        switch (gameMode_)
        {
            case GameMode.Setup:
                {
                    // random team goes first
                    currentTeam = Random.Range(0, 1);
                    for (int i = 0; i < NumTeams; ++i)
                        teamBalls [i] = BallsPerTeam;
                
                    // get rid of all the previous balls
                    IEnumerable<GameObject> gameObjects = GameObject.FindGameObjectsWithTag("BocceBall");
                    foreach (GameObject gameObject in gameObjects)
                        GameObject.Destroy(gameObject);
                    jack = null;
                
                    SetMessageText("Throwing the jack!");
                    break;
                }
            case GameMode.Aiming:
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    
                    SetMessageText("Team " + (currentTeam + 1) + "'s turn!", currentTeam);
                    SetHintMessageText("Press SPACE to throw ball.");
                    break;
                }
            case GameMode.AimingFinish:
                {
                    currentWaitTime = minWaitBetweenThrows;
                
                    SetMessageText("Waiting for balls to stop...");
                    SetHintMessageText("");
                    break;
                }
            case GameMode.RoundResult:
                {                
                    int winningTeam = GetClosestTeam();
                    int losingTeam = (winningTeam == 0 ? 1 : 0);
                    
                    List<float> winningDistances = GetBallDistances(winningTeam);
                    List<float> losingDistances = GetBallDistances(losingTeam);
                    
                    int points;
                    if (losingDistances.Count() == 0) // how'd this player throw all their balls out of bounds?
                        points = winningDistances.Count();
                    else
                        points = winningDistances.Count(distanceSq => distanceSq < losingDistances.Min());
                    teamScore [winningTeam] += points;
                
                    SetTeamScore(winningTeam, teamScore [winningTeam]);
                    SetMessageText("Team " + (winningTeam + 1).ToString() + " scores " + points.ToString() + " points!");
                    break;
                }
            case GameMode.GameResult:
                {
                    int winningTeam = 0;
                    int highScore = 0;
                    for (int i = 0; i < NumTeams; ++i)
                    {
                        if (teamScore [i] > highScore)
                        {
                            highScore = teamScore [i];
                            winningTeam = i;
                        }
                    }
                
                    SetMessageText("Team " + (winningTeam + 1).ToString() + " wins!", winningTeam);
                    SetHintMessageText("Press SPACE to return to main menu.");
                    break;
                }
        }
        
        MainCamera.gameObject.SetActive(gameMode_ != GameMode.Aiming);
        PlayerCamera.gameObject.SetActive(gameMode_ == GameMode.Aiming);
        BallForceBar.gameObject.SetActive(gameMode_ == GameMode.Aiming);
    
        prevGameMode = gameMode;
        gameMode = gameMode_;
    }
    
    #region SetMessageText
    
    void SetMessageText(string message_)
    {
        SetMessageText(message_, -1);
    }
    
    void SetMessageText(string message_, int team)
    {
        MessageText.GetComponent<GUIText>().text = message_;
        MessageText.GetComponent<GUIText>().color = GetTeamColor(team);
    }
    
    #endregion
    
    void SetHintMessageText(string message_)
    {
        HintMessageText.GetComponent<GUIText>().text = message_;
    }
    
    #region CreateBall
    
    Rigidbody CreateBall()
    {
        return CreateBall(-1);
    }
    
    Rigidbody CreateBall(int team_)
    {
        return CreateBall(team_, PlayerCamera.gameObject.transform.position);
    }
    
    Rigidbody CreateBall(int team_, Vector3 position_)
    {
        Rigidbody rigidBody = (Rigidbody)Instantiate(BocceBallPrefab, position_, new Quaternion());
        BocceBall component = rigidBody.GetComponent<BocceBall>();
        if (component)
            component.Team = team_;
        rigidBody.GetComponent<Renderer>().material.color = GetTeamColor(team_);            
        return rigidBody;
    }
    
    #endregion
    
    void ThrowBall(Vector3 force)
    {
        currentBall.AddForce(force);
    } 
}