using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Hollow_Knight_Mode : MonoBehaviour
{
    SpriteRenderer spr;
    [SerializeField] bool visible;
    private void Awake() {
        spr = GetComponent<SpriteRenderer>();
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.H)) {
            visible = !visible;
        }

        spr.enabled = visible;
    }
}
