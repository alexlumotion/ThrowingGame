using UnityEngine;
using UnityEngine.EventSystems;

public class ClickRouter : MonoBehaviour
{
    [Header("Refs")]
    public Camera cam;

    [Header("Options")]
    public bool ignoreUI = true;
    public float rayMaxDistance = 100f;

    void Awake()
    {
        if (!cam) cam = Camera.main;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            HandleClick(Input.mousePosition);
        }           
    }

    void HandleClick(Vector3 screenPos)
    {

        // 1) ігноруємо кліки по UI
        if (ignoreUI && EventSystem.current && EventSystem.current.IsPointerOverGameObject())
            return;


        // 2) рейкаст у сцену
        Ray ray = cam.ScreenPointToRay(screenPos);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, rayMaxDistance))
        {
            if (hit.collider.tag == "Shell")
            {
                //Debug.Log("SHELL: " + hit.transform.position);
                //hit.transform.GetComponent<Shape3D>().OnClick(new Vector3(hit.point.x, hit.point.y + 2, hit.point.z));
                hit.transform.GetComponent<Shape3D>().OnClick(new Vector3(hit.point.x, hit.point.y + 1, -3f));
            }
            else if (hit.collider.tag == "Background")
            {
                Vector3 newVector = new Vector3(hit.point.x, hit.point.y + 1, hit.point.z);
                BubbleFlipbookPool.Instance.StartFlipbook(newVector);
            }
        }
    }
}