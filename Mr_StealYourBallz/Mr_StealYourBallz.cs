
  
using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class Mr_StealYourBallz : CogsAgent
{
    // ------------------BASIC MONOBEHAVIOR FUNCTIONS-------------------
    
    // Initialize values
    protected override void Start()
    {
        base.Start();
        AssignBasicRewards();
    }

    // For actual actions in the environment (e.g. movement, shoot laser)
    // that is done continuously
    protected override void FixedUpdate() {
        base.FixedUpdate();
        
        LaserControl();
        // Movement based on DirToGo and RotateDir
        moveAgent(dirToGo, rotateDir);
    }


    
    // --------------------AGENT FUNCTIONS-------------------------

    // Get relevant information from the environment to effectively learn behavior
    public override void CollectObservations(VectorSensor sensor)
    {
        // Agent velocity in x and z axis 
        var localVelocity = transform.InverseTransformDirection(rBody.velocity);
        sensor.AddObservation(localVelocity.x);
        sensor.AddObservation(localVelocity.z);

       
        // enemy's position 
        sensor.AddObservation(enemy.transform.localPosition);


        // Time remaning
        sensor.AddObservation(timer.GetComponent<Timer>().GetTimeRemaining());  

        // Agent's current rotation
        var localRotation = transform.rotation;
        sensor.AddObservation(transform.rotation.y);

        // Agent and home base's position
        sensor.AddObservation(this.transform.localPosition);
        sensor.AddObservation(baseLocation.localPosition);

        // for each target in the environment, add: its position, whether it is being carried,
        // and whether it is in a base
        foreach (GameObject target in targets){
            sensor.AddObservation(target.transform.localPosition);
            sensor.AddObservation(target.GetComponent<Target>().GetCarried());
            sensor.AddObservation(target.GetComponent<Target>().GetInBase());
        }

        //targets in our base minus target in enemy's base (score)
        sensor.AddObservation(myBase.GetComponent<HomeBase>().GetCaptured() - GetEnemyCaptured());
        
        // Whether the agent is frozen
        sensor.AddObservation(IsFrozen());

        //position of the nearest target 
        sensor.AddObservation(GetNearestTarget().transform.localPosition);

        //distance between agent and base
        sensor.AddObservation(DistanceToBase());

        //number of targets enemy is carrying
        sensor.AddObservation(GetEnemyCarried());
    }

    // For manual override of controls. This function will use keyboard presses to simulate output from your NN 
    public override void Heuristic(float[] actionsOut)
    {
        var discreteActionsOut = actionsOut;
        discreteActionsOut[0] = 0; //Simulated NN output 0
        discreteActionsOut[1] = 0; //....................1
        discreteActionsOut[2] = 0; //....................2
        discreteActionsOut[3] = 0; //....................3

        //TODO-2: Uncomment this next line when implementing GoBackToBase();
        discreteActionsOut[4] = 0;

       
        if (Input.GetKey(KeyCode.UpArrow))
        {
            discreteActionsOut[0] = 1;
        }       
        if (Input.GetKey(KeyCode.DownArrow))
        {
            discreteActionsOut[0] = 2;
        }
        if (Input.GetKey(KeyCode.RightArrow))
        {
            discreteActionsOut[1] = 1;
        }
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            //TODO-1: Using the above as examples, set the action out for the left arrow press
            discreteActionsOut[1] = 2;
            
        }
        

        //Shoot
        if (Input.GetKey(KeyCode.Space)){
            discreteActionsOut[2] = 1;
        }

        //GoToNearestTarget
        if (Input.GetKey(KeyCode.A)){
            discreteActionsOut[3] = 1;
        }


        //TODO-2: implement a keypress (your choice of key) for the output for GoBackToBase();
        if (Input.GetKey(KeyCode.B)) {
            discreteActionsOut[4] = 1;
        }

    }

        // What to do when an action is received (i.e. when the Brain gives the agent information about possible actions)
    public override void OnActionReceived(float[] act)
    {
        int forwardAxis = (int)act[0]; //NN output 0

        //TODO-1: Set these variables to their appopriate item from the act list
        int rotateAxis = (int)act[1]; 
        int shootAxis = (int)act[2]; 
        int goToTargetAxis = (int)act[3];
        
        //TODO-2: Uncomment this next line and set it to the appropriate item from the act list
        int goToBaseAxis = (int)act[4];

        //TODO-2: Make sure to remember to add goToBaseAxis when working on that part!
        
        MovePlayer(forwardAxis, rotateAxis, shootAxis, goToTargetAxis, goToBaseAxis);

        

    }


// ----------------------ONTRIGGER AND ONCOLLISION FUNCTIONS------------------------
    // Called when object collides with or trigger (similar to collide but without physics) other objects
    protected override void OnTriggerEnter(Collider collision)
    {
        

        
        if (collision.gameObject.CompareTag("HomeBase") && collision.gameObject.GetComponent<HomeBase>().team == GetTeam())
        {
            
            if (GetCarrying() > 0) {
                AddReward(1.0f);
            } else {
                AddReward(-0.1f);
            }

        }
        base.OnTriggerEnter(collision);
    }

    protected override void OnCollisionEnter(Collision collision) 
    {
        

        //target is not in my base and is not being carried and I am not frozen
        if (collision.gameObject.CompareTag("Target") && collision.gameObject.GetComponent<Target>().GetInBase() != GetTeam() && collision.gameObject.GetComponent<Target>().GetCarried() == 0 && !IsFrozen())
        {
            //Removed this reward because agent tried to grab all the balls 
            //AddReward(1.0f);
        }

        if (collision.gameObject.CompareTag("Wall"))
        {
            
            AddReward(-2f);
        }
        base.OnCollisionEnter(collision);
    }



    //  --------------------------HELPERS---------------------------- 
     private void AssignBasicRewards() {
        rewardDict = new Dictionary<string, float>();

        rewardDict.Add("frozen", -1.0f); //-1
        rewardDict.Add("shooting-laser", -0.3f); //-0.3
        rewardDict.Add("hit-enemy", 2.0f); //2
        rewardDict.Add("dropped-one-target", 0f);
        rewardDict.Add("dropped-targets", 0f);
    }
    
    private void MovePlayer(int forwardAxis, int rotateAxis, int shootAxis, int goToTargetAxis, int goToBaseAxis)
    //TODO-2: Add goToBase as an argument to this function ^
    {
        dirToGo = Vector3.zero;
        rotateDir = Vector3.zero;

        Vector3 forward = transform.forward;
        Vector3 backward = -transform.forward;
        Vector3 right = transform.up;
        Vector3 left = -transform.up;

        //fowardAxis: 
            // 0 -> do nothing
            // 1 -> go forward
            // 2 -> go backward
        if (forwardAxis == 0){
            //do nothing. This case is not necessary to include, it's only here to explicitly show what happens in case 0
        }
        else if (forwardAxis == 1){
            dirToGo = forward;
        }
        else if (forwardAxis == 2){
            //TODO-1: Tell your agent to go backward!
            dirToGo = backward;
            
        }

        //rotateAxis: 
            // 0 -> do nothing
            // 1 -> go right
            // 2 -> go left
        if (rotateAxis == 0){
            //do nothing
        }
        
        //TODO-1 : Implement the other cases for rotateDir
        if (rotateAxis == 1) {
            rotateDir = right;
        }

        if (rotateAxis == 2) {
            rotateDir = left;
        }

        //shoot
        if (shootAxis == 1){
            SetLaser(true);
        }
        else {
            SetLaser(false);
        }


     //go to the nearest target 
        if (goToTargetAxis == 1){
            GoToNearestTarget();
            
        }
        

        //TODO-2: Implement the case for goToBaseAxis 
        if (goToBaseAxis == 1) {
            GoToBase();

        }

        // go get the closest target
        if(GetCarrying() == 0){
            GoToNearestTarget();
            AddReward(1 / DistanceToNearTarget());
        }
        
        //If our agent is already carrying a target and another target is closer than the home base, grab the target before going to home base
        if(GetCarrying() == 1) {
            if (DistanceToBase() > DistanceToNearTarget()) {
                GoToNearestTarget();
                AddReward(1 / DistanceToNearTarget());
            }
            if (DistanceToBase() < DistanceToNearTarget()) {
                GoToBase();
                AddReward(1 / DistanceToBase());
            }
        } 

        //If our own agent is carrying more than 1 target, add reward for getting closer to the base to 
        //incentivize it to return to base and not grab too many balls at once
        if (GetCarrying() > 1) {
            GoToBase();
            AddReward(1 / DistanceToBase());
        }


        //If the enemy has captured more targets give agent a negative reward, if we capture more give positive reward
        if (GetEnemyCaptured() > myBase.GetComponent<HomeBase>().GetCaptured()) {
            AddReward(-0.5f);
        } else {
            AddReward(1.5f);
        }
        
        //If the amount of targets we are carrying is more than opponent add reward, otherwise add negative reward
        if(GetCarrying() > GetEnemyCarried()) {
            AddReward(0.5f);
        } else {
            AddReward(-0.5f);
        }

        //If the opponent is carrying more than 2 balls and they are in front of you, 
        //call hunt enemy which shoots them
        if (GetEnemyCarried() > 2) {
            if (GetYAngle(enemy) > -45 && GetYAngle(enemy) < 45) {
                HuntEnemy();
            }
            
        }

        //End game strategy:
        //First return any carrying balls back to own base
        // Then go to the enemy's base and steal their balls if they have balls, 
        //otherwise continue collecting balls
        if (timer.GetComponent<Timer>().GetTimeRemaining() <= 30) {
            if (GetCarrying() > 0) {
                GoToBase();
            }
            if (GetEnemyCaptured() > 0) {
                int prev = GetEnemyCaptured();
                GoToEnemyTargets(); 
                if (GetEnemyCaptured() < prev) {
                    AddReward(2.0f);
                } 
            } else {
                GoToNearestTarget();
            }

        }
          
        
    }

    //Go to a target that belongs to the enemy 
    private void GoToEnemyTargets(){
        GameObject target = GetEnemyTarget();
        if (target != null) {
            float rotation = GetYAngle(target);
            TurnAndGo(rotation);   
        }
           
    }

    // Go to home base
    private void GoToBase(){
        TurnAndGo(GetYAngle(myBase));
    }

    // Go to the nearest target
    private void GoToNearestTarget(){
        GameObject target = GetNearestTarget();
        if (target != null){
            float rotation = GetYAngle(target);
            TurnAndGo(rotation);
        }        
    }

    // Rotate and go in specified direction
    private void TurnAndGo(float rotation){

        if(rotation < -5f){
            rotateDir = transform.up;
        }
        else if (rotation > 5f){
            rotateDir = -transform.up;
        }
        else {
            dirToGo = transform.forward;
        }
    }
    

     // Go to the enemy 
    private void GoToEnemy(){
        float rotation = GetYAngle(enemy);
        if (rotation > 45 || rotation < -45) {
            TurnAndGo(rotation);
        }
        else if (rotation < 45 || rotation < -45) {
            GoWhileTurning(rotation);
        }

    }

    //Go to the enemy and shoot them
    private void HuntEnemy(){
        if (DistanceToEnemy() >= 20){
            GoToEnemy();
        }
        if (DistanceToEnemy() < 20){
            TurnAndShoot();
        }

    }
        // return the distance between agent and nearest target
    private float DistanceToNearTarget(){
        if (GetNearestTarget() != null) {
           float distance = Vector3.Distance(GetNearestTarget().transform.localPosition, transform.localPosition);
            return distance; 
        }
        else {
            return 1.0f;
        }
    }

    //return the distane to the enemy
    private float DistanceToEnemy(){
        float distance = Vector3.Distance(enemy.transform.localPosition, transform.localPosition);
        return distance;
    }

    //rotate the agent and shoot
    private void TurnAndShoot(){
        float rotation = GetYAngle(enemy);
        if (rotation < -5f){
            rotateDir = transform.up;
        }
        else if (rotation > 5f){
            rotateDir = -transform.up;
        }
        else {
            SetLaser(true);
        }
        
    }
 
    //similar to TurnAndGo where the agent moves while turning 
    private void GoWhileTurning(float rotation){
        dirToGo = transform.forward;
        if(rotation < -5f){
            rotateDir = transform.up;
        }
        else if (rotation > 5f){
            rotateDir = -transform.up;
        }
    }

    // return reference to nearest target (ball) 
    //but if there are no targets then nearest target is the base so that observations do not come back as a null exception
    protected GameObject GetNearestTarget(){
        float distance = 200;
        GameObject nearestTarget = myBase;
        foreach (var target in targets)
        {
            float currentDistance = Vector3.Distance(target.transform.localPosition, transform.localPosition);
            if (currentDistance < distance && target.GetComponent<Target>().GetCarried() == 0 && target.GetComponent<Target>().GetInBase() != team){
                distance = currentDistance;
                nearestTarget = target;
            }
        }
        return nearestTarget;
    }

    private float GetYAngle(GameObject target) {
        
       Vector3 targetDir = target.transform.position - transform.position;
       Vector3 forward = transform.forward;

      float angle = Vector3.SignedAngle(targetDir, forward, Vector3.up);
      return angle; 
        
    }

    //returm the number of targets that the enemy has captured in their base 
     private int GetEnemyCaptured(){
        int enemyCaptured = 0;
        //int enemyCarry = 0;
        //int available = 0;
        foreach(GameObject target in targets){

            int inBase = target.GetComponent<Target>().GetInBase();

            //if they are not in your base, and not in no one's base, they must be in the enemy base!
            if (inBase != team && inBase != 0){
                enemyCaptured += 1;
            }

        }

        return enemyCaptured;
    }

        //return the number of targets the enemy is carrying
        private int GetEnemyCarried(){
        int enemyCarry = 0;
        foreach(GameObject target in targets){

            int carried = target.GetComponent<Target>().GetCarried();

            if (carried != team && carried != 0){
                enemyCarry += 1;
            }

        }

        return enemyCarry;
    }
        //go to the closest target that belongs to the enemy
        private GameObject GetEnemyTarget(){
        float distance = 200;
        GameObject nearestEnemyTarget = null;
        foreach (var target in targets)
        {
            float currentDistance = Vector3.Distance(target.transform.localPosition, transform.localPosition);
            if (currentDistance < distance && target.GetComponent<Target>().GetCarried() == 0 && target.GetComponent<Target>().GetInBase() != GetTeam() 
            && target.GetComponent<Target>().GetInBase() != 0){
                distance = currentDistance;
                nearestEnemyTarget = target;
            }
        }
        return nearestEnemyTarget;
    }

    

}

