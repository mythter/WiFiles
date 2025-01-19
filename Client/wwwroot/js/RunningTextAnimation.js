export class RunningTextAnimation {

    static speed;
    static delay;

    static Init(container, element, animationSpeed = 12, animationDelay = 3000) {
        this.speed = animationSpeed;
        this.delay = animationDelay;
        let containerWidth = this.GetContainerWidth(container);
        document.documentElement.style.setProperty("--container-width", containerWidth + "px");

        this.SetAnimation(element, container);

        new ResizeObserver(() => {
            let containerWidth = this.GetContainerWidth(container);
            document.documentElement.style.setProperty("--container-width", containerWidth + "px");
            this.CheckAnimation(element, container);
        }).observe(container);
    }

    static CheckAnimation(elem, container) {
        let containerWidth = this.GetContainerWidth(container);
        elem.classList.remove("running-text");
        if (elem.clientWidth > containerWidth) {
            elem.classList.add("running-text");
            this.SetAnimationDuration(elem, container);
        }
    }

    static SetAnimation(elem, container) {
        elem.classList.add("running-text");

        this.SetAnimationDuration(elem, container)

        elem.addEventListener("animationiteration", () => {
            elem.style["animation-play-state"] = "paused";
            setTimeout(function () {
                elem.style["animation-play-state"] = "running";
            }, this.delay);
        });
    }

    static SetAnimationDuration(elem, container) {
        let containerWidth = this.GetContainerWidth(container);
        let duration = (elem.clientWidth - containerWidth) / this.speed;
        elem.style["animation-duration"] = duration + "s";
    }

    static GetContainerWidth(container) {
        let infoPaddingRight = parseFloat(window.getComputedStyle(container, null).getPropertyValue('padding-right'));
        let infoPaddingLeft = parseFloat(window.getComputedStyle(container, null).getPropertyValue('padding-left'));
        return container.clientWidth - infoPaddingRight - infoPaddingLeft;
    }
}