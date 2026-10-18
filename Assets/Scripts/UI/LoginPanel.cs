using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoginPanel : BasePanel
{
    [SerializeField]
    private Button loginBtn;

    [SerializeField]
    private Button registerBtn;

    [SerializeField]
    private Button exitBtn;

    [SerializeField]
    private Button returnBtn;

    [SerializeField]
    private Button sureBtn;

    [SerializeField]
    private GameObject startObj;

    [SerializeField]
    private GameObject nextObj;

    [SerializeField]
    private TMP_InputField accountInput;

    [SerializeField]
    private TMP_InputField passwordInput;

    private bool isRegisterMode;

    void Start()
    {
        startObj.SetActive(true);
        nextObj.SetActive(false);
        loginBtn.onClick.AddListener(OnClickLogin);
        registerBtn.onClick.AddListener(OnClickRegister);
        exitBtn.onClick.AddListener(() =>
        {
            Application.Quit();
        });
        returnBtn.onClick.AddListener(OnClickReturn);
        sureBtn.onClick.AddListener(OnClickSure);
    }

    private void OnClickLogin()
    {
        isRegisterMode = false;
        OnClickStart();
    }

    private void OnClickRegister()
    {
        isRegisterMode = true;
        OnClickStart();
    }

    private void OnClickStart()
    {
        accountInput.text = "";
        passwordInput.text = "";
        startObj.SetActive(false);
        nextObj.SetActive(true);
    }


    private void OnClickReturn()
    {
        startObj.SetActive(true);
        nextObj.SetActive(false);
    }

    private void OnClickSure()
    {
        string account = accountInput.text.Trim();
        string password = passwordInput.text;
        
        if (!NetManager.Instance.IsConnected)
        {
            NetManager.Instance.Connect();
        }

        if (!NetManager.Instance.IsConnected)
        {
            return;
        }

        if (isRegisterMode)
        {
            NetManager.Instance.Register(account, password);
        }
        else
        {
            NetManager.Instance.Login(account, password);
        }
    }
}
