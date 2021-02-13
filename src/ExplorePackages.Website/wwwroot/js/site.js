$(function () {
    $('input[name="useCustomMax"]').on('click', function () {
        $(this).parent().parent().siblings('.custom-max').toggleClass('hidden')
    });
})
