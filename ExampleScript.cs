using UnityEngine;

namespace BBCustomScripts
{
    class ExampleScript : ScriptBaseClass
    {
        //REQUIRED: tag that identifies which objects to apply script to, "C__" ...
        public static string[] objectTags = new string[] { "SpinningCube" };

        public Vector3 resetPosition;
        public Quaternion resetRotation;

        //Unity function, called when object is created
        private void Start()
        {
            resetPosition = transform.position;
            resetRotation = transform.rotation; 
        }

        //REQUIRED: function that returns the objectTags variable
        public static string[] GetTags()
        {
            return objectTags;
        }

        //REQUIRED: called when a new round starts, any code can be used here
        public override void ResetObject()
        {
            transform.position = resetPosition;
            transform.rotation = resetRotation;
        }

        //Unity function, called once every frame
        private void Update()
        {
            transform.position += new Vector3(0.01f, 0, 0);
            transform.rotation = UnityEngine.Random.rotation;
        }
    }
}
